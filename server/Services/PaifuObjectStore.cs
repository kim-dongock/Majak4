using Amazon;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;

namespace MajakServer.Services;

public sealed class PaifuObjectStore
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly int _presignedUrlMinutes;
    private readonly string? _kmsKeyId;

    public PaifuObjectStore(IConfiguration configuration, IHostEnvironment environment)
    {
        _bucket = configuration["Paifu:S3Bucket"]?.Trim() ?? "";
        _prefix = configuration["Paifu:S3Prefix"]?.Trim().Trim('/') ?? "paifu";
        _presignedUrlMinutes = Math.Clamp(configuration.GetValue("Paifu:PresignedUrlMinutes", 5), 1, 15);
        _kmsKeyId = configuration["Paifu:KmsKeyId"]?.Trim();
        var region = RegionEndpoint.GetBySystemName(configuration["AWS:Region"] ?? "ap-northeast-1");
        var profileName = environment.IsDevelopment() ? configuration["AWS:ProfileName"]?.Trim() : null;
        if (!string.IsNullOrWhiteSpace(profileName)
            && new CredentialProfileStoreChain().TryGetAWSCredentials(profileName, out var credentials))
        {
            _s3 = new AmazonS3Client(credentials, region);
        }
        else
        {
            _s3 = new AmazonS3Client(region);
        }
    }

    public string CreateObjectKey(DateTimeOffset playedAt)
        => $"{_prefix}/{playedAt:yyyy/MM/dd}/{Guid.NewGuid():N}.json.br";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_bucket);

    public async Task PutAsync(string objectKey, byte[] compressedPayload, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var stream = new MemoryStream(compressedPayload, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = "application/json",
            ServerSideEncryptionMethod = string.IsNullOrWhiteSpace(_kmsKeyId)
                ? ServerSideEncryptionMethod.AES256
                : ServerSideEncryptionMethod.AWSKMS,
            ServerSideEncryptionKeyManagementServiceKeyId = _kmsKeyId,
        };
        request.Headers.ContentEncoding = "br";
        await _s3.PutObjectAsync(request, cancellationToken);
    }

    public string CreateReplayUrl(string objectKey)
    {
        EnsureConfigured();
        return _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_presignedUrlMinutes),
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentType = "application/json",
                ContentEncoding = "br",
            },
        });
    }

    public async Task<byte[]> GetAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var response = await _s3.GetObjectAsync(_bucket, objectKey, cancellationToken);
        using var output = new MemoryStream();
        await response.ResponseStream.CopyToAsync(output, cancellationToken);
        return output.ToArray();
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        return _s3.DeleteObjectAsync(_bucket, objectKey, cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new InvalidOperationException("Paifu:S3Bucket is not configured.");
    }
}
