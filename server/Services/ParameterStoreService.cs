using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using MajakServer.Models;
using MajakServer.Utils;

namespace MajakServer.Services;

/// <summary>
/// AWS Parameter Store から接続情報を取得するサービス
/// CEServiceAgent の ConfigurationService と同一の方式で実装
/// </summary>
public interface IParameterStoreService
{
    Task<AwsParameterConfig> GetParameterConfigAsync();
}

public class ParameterStoreService : IParameterStoreService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ParameterStoreService> _logger;
    private readonly IHostEnvironment _environment;

    // 起動時に一度だけ取得して永続キャッシュ — AWS SSM は起動時の 1 回のみ呼ぶ。
    // 認証情報を更新する場合はサーバーを再起動すること。
    private AwsParameterConfig? _cachedConfig;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public ParameterStoreService(
        IConfiguration configuration,
        ILogger<ParameterStoreService> logger,
        IHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// AWS SSM クライアントを生成する
    /// - Development: ~/.aws/credentials の "majak2" Named Profile を使用
    /// - その他: EC2 IAM Role (インスタンスプロファイル → 環境変数 → 認証情報ファイル)
    /// </summary>
    private IAmazonSimpleSystemsManagement CreateSsmClient()
    {
        try
        {
            var region = Amazon.RegionEndpoint.GetBySystemName(
                _configuration["AWS:Region"] ?? "ap-northeast-1");

            _logger.LogInformation("========================================");
            _logger.LogInformation("Creating AWS SSM Client");
            _logger.LogInformation("Environment: {Environment}", _environment.EnvironmentName);
            _logger.LogInformation("Region: {Region}", region.SystemName);

            if (_environment.IsDevelopment())
            {
                const string profileName = "majak2";
                var chain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
                if (chain.TryGetAWSCredentials(profileName, out var profileCredentials))
                {
                    _logger.LogInformation("Authentication: Named Profile \"{Profile}\"", profileName);
                    _logger.LogInformation("========================================");
                    return new AmazonSimpleSystemsManagementClient(profileCredentials, region);
                }
                _logger.LogWarning("Named profile \"{Profile}\" not found. Falling back to default credential chain.", profileName);
            }
            else
            {
                _logger.LogInformation("Authentication: EC2 IAM Role (all environments)");
                _logger.LogInformation("Credential Chain: 1) EC2 Instance Profile, 2) Environment Variables, 3) Credential File");
            }

            _logger.LogInformation("========================================");
            return new AmazonSimpleSystemsManagementClient(region);
        }
        catch (Exception ex)
        {
            _logger.LogError("========================================");
            _logger.LogError("FAILED TO CREATE AWS SSM CLIENT");
            _logger.LogError("========================================");
            _logger.LogError("Error Type: {ErrorType}", ex.GetType().FullName);
            _logger.LogError("Error Message: {Message}", ex.Message);
            if (ex.InnerException != null)
                _logger.LogError("Inner Exception: {InnerException}", ex.InnerException.Message);
            _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
            _logger.LogError("========================================");
            throw;
        }
    }

    public async Task<AwsParameterConfig> GetParameterConfigAsync()
    {
        // 永続キャッシュヒット: 起動後 2 回目以降は AWS SSM を呼ばない
        if (_cachedConfig is not null)
            return _cachedConfig;

        await _cacheLock.WaitAsync();
        try
        {
            // ダブルチェック (ロック取得待ちの間に他スレッドがロード済みの場合)
            if (_cachedConfig is not null)
                return _cachedConfig;

            // TODO: AWS Parameter Store 接続を一時的に無効化。appsettings.json の ConnectionStrings を使用する。
            // var result = await FetchFromSsmAsync();
            // _cachedConfig = result;
            // _logger.LogInformation("ParameterStoreService: config loaded and cached permanently (restart to refresh).");
            _logger.LogWarning("ParameterStoreService: AWS Parameter Store is DISABLED. Falling back to appsettings.json ConnectionStrings.");
            _cachedConfig = new AwsParameterConfig();
            return _cachedConfig;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<AwsParameterConfig> FetchFromSsmAsync()
    {
        var paramPath = _configuration["AWS:ParameterStore:Path"] ?? "/config/application/majak";

        try
        {
            _logger.LogInformation("Creating AWS SSM client for environment: {Environment}", _environment.EnvironmentName);

            using var ssm = CreateSsmClient();

            var request = new GetParameterRequest
            {
                Name = paramPath,
                WithDecryption = true
            };

            _logger.LogInformation("Retrieving configuration from AWS Parameter Store at {Path}", paramPath);
            var response = await ssm.GetParameterAsync(request);

            _logger.LogInformation("Parameter retrieved successfully. Name: {Name}, Type: {Type}, Version: {Version}",
                response.Parameter.Name,
                response.Parameter.Type,
                response.Parameter.Version);

            var parameterValue = response.Parameter.Value;

            if (string.IsNullOrEmpty(parameterValue))
                throw new InvalidOperationException("AWS Parameter Store returned empty value");

            _logger.LogInformation("Parameter value length: {Length}", parameterValue.Length);

            // JSON を compact 化 (空白・改行の差異を吸収)
            parameterValue = Regex.Replace(parameterValue, @"\s+", " ");
            parameterValue = Regex.Replace(parameterValue, @"\s*{\s*", "{");
            parameterValue = Regex.Replace(parameterValue, @"\s*}\s*", "}");
            parameterValue = Regex.Replace(parameterValue, @"\s*:\s*", ":");
            parameterValue = Regex.Replace(parameterValue, @"\s*,\s*", ",");
            parameterValue = parameterValue.Trim();

            _logger.LogInformation("After minifying JSON, length: {Length}, First 100 chars: {Preview}",
                parameterValue.Length,
                parameterValue.Length > 100 ? parameterValue.Substring(0, 100) : parameterValue);

            // まず JSON としてパースを試みる
            AwsParameterConfig? config = null;
            try
            {
                _logger.LogInformation("Attempting to parse as JSON...");

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };

                config = JsonSerializer.Deserialize<AwsParameterConfig>(parameterValue, jsonOptions);

                if (config != null)
                {
                    _logger.LogInformation("Parameter is valid JSON with encrypted field values");
                    return config;
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogInformation("JSON parsing failed: {Message}, will try to decrypt entire value", jsonEx.Message);
            }

            // JSON パース失敗 → 全体を復号 (レガシー形式)
            _logger.LogInformation("Attempting to decrypt entire parameter value...");
            var decryptedValue = CryptoHelper.DecryptParameterStoreValue(parameterValue);

            if (string.IsNullOrEmpty(decryptedValue))
            {
                _logger.LogError("Decryption failed - returned empty string. Original length: {Length}", parameterValue.Length);
                throw new InvalidOperationException("Failed to decrypt AWS parameter value");
            }

            _logger.LogInformation("Parameter value decrypted successfully. Decrypted length: {Length}", decryptedValue.Length);

            return JsonSerializer.Deserialize<AwsParameterConfig>(decryptedValue)
                ?? throw new InvalidOperationException("Failed to deserialize decrypted AWS parameter config");
        }
        catch (ParameterNotFoundException pnfEx)
        {
            _logger.LogError("========================================");
            _logger.LogError("AWS PARAMETER NOT FOUND");
            _logger.LogError("========================================");
            _logger.LogError("Parameter Path: {Path}", paramPath);
            _logger.LogError("Error Type: ParameterNotFoundException");
            _logger.LogError("Error Message: {Message}", pnfEx.Message);
            _logger.LogError("Error Code: {ErrorCode}", pnfEx.ErrorCode);
            _logger.LogError("Status Code: {StatusCode}", pnfEx.StatusCode);
            _logger.LogError("Stack Trace: {StackTrace}", pnfEx.StackTrace);
            _logger.LogError("========================================");
            throw new InvalidOperationException(
                $"AWS Parameter not found: {paramPath}. Please create the parameter in AWS Systems Manager.", pnfEx);
        }
        catch (AmazonSimpleSystemsManagementException awsEx)
        {
            _logger.LogError("========================================");
            _logger.LogError("AWS SSM ERROR");
            _logger.LogError("========================================");
            _logger.LogError("Parameter Path: {Path}", paramPath);
            _logger.LogError("Error Type: {ErrorType}", awsEx.GetType().Name);
            _logger.LogError("Error Message: {Message}", awsEx.Message);
            _logger.LogError("Error Code: {ErrorCode}", awsEx.ErrorCode);
            _logger.LogError("Status Code: {StatusCode}", awsEx.StatusCode);

            if (awsEx.ErrorCode?.Contains("AccessDenied") == true ||
                awsEx.ErrorCode?.Contains("UnauthorizedAccess") == true ||
                awsEx.Message?.Contains("not authorized") == true)
            {
                _logger.LogError("IAM PERMISSION ISSUE DETECTED");
                _logger.LogError("Required permission: ssm:GetParameter");
                _logger.LogError("Resource: arn:aws:ssm:ap-northeast-1:*:parameter{Path}", paramPath);
                _logger.LogError("Please attach proper IAM Role to this EC2 instance");
            }

            if (awsEx.InnerException != null)
                _logger.LogError("Inner Exception: {InnerException}", awsEx.InnerException.Message);

            _logger.LogError("Stack Trace: {StackTrace}", awsEx.StackTrace);
            _logger.LogError("========================================");
            throw new InvalidOperationException(
                $"AWS SSM error: {awsEx.Message}. Check AWS credentials and IAM permissions.", awsEx);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError("JSON PARSING ERROR: {Message}", jsonEx.Message);
            throw new InvalidOperationException("Failed to parse AWS parameter value as JSON. Check parameter format.", jsonEx);
        }
        catch (Exception ex)
        {
            _logger.LogError("========================================");
            _logger.LogError("UNEXPECTED ERROR");
            _logger.LogError("========================================");
            _logger.LogError("Parameter Path: {Path}", paramPath);
            _logger.LogError("Error Type: {ErrorType}", ex.GetType().FullName);
            _logger.LogError("Error Message: {Message}", ex.Message);
            if (ex.InnerException != null)
                _logger.LogError("Inner Exception: {InnerException}", ex.InnerException.Message);
            _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
            _logger.LogError("========================================");
            throw;
        }
    }
}
