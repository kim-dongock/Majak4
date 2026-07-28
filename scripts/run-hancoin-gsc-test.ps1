param(
    [string]$InquiryUrl = "http://alpha-gsc.hange.jp/bill/inquiry",
    [string]$ExecuteUrl = "http://alpha-gsc.hange.jp/bill/execute"
)

$memberId = Read-Host "HANCOIN_TEST_MEMBER_ID"
$securePassword = Read-Host "HANCOIN_TEST_PASSWORD" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)

try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    if ([string]::IsNullOrWhiteSpace($memberId) -or [string]::IsNullOrWhiteSpace($plainPassword)) {
        Write-Error "HANCOIN_TEST_MEMBER_ID and HANCOIN_TEST_PASSWORD are required."
        exit 1
    }

    $env:HANCOIN_TEST_MEMBER_ID = $memberId
    $env:HANCOIN_TEST_PASSWORD = $plainPassword
    $env:HANCOIN_TEST_INQUIRY_URL = $InquiryUrl
    $env:HANCOIN_TEST_EXECUTE_URL = $ExecuteUrl

    dotnet test server.tests/MajakServer.Tests.csproj --filter FullyQualifiedName~HanCoinServiceTests.InquiryAsync_WithConfiguredRealUser_ReturnsGscBalance -p:UseAppHost=false --logger "console;verbosity=detailed"
}
finally {
    if ($bstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
    Remove-Item Env:\HANCOIN_TEST_MEMBER_ID -ErrorAction SilentlyContinue
    Remove-Item Env:\HANCOIN_TEST_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\HANCOIN_TEST_INQUIRY_URL -ErrorAction SilentlyContinue
    Remove-Item Env:\HANCOIN_TEST_EXECUTE_URL -ErrorAction SilentlyContinue
}