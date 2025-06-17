using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Botatwork_in_Livechat.Models;

namespace Botatwork_in_Livechat.Services
{
    public interface ITokenService
    {
        Task<bool> RefreshTokenIfNeeded();
        Task<string> GetClientId(string accessToken);
        Task<string> GetUserEmail(string accessToken);
    }

    public class TokenService : ITokenService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TokenService> _logger;
        private readonly HttpClient _httpClient;
        private const string API_BASE_URL = "https://api.botatwork.com";

        public TokenService(
            IHttpContextAccessor httpContextAccessor,
            ILogger<TokenService> logger,
            HttpClient httpClient)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _httpClient = httpClient;
        }


        public async Task<string> GetClientId(string accessToken)
        {
            try
            {
                // First check session
                var sessionClientId = _httpContextAccessor.HttpContext?.Session.GetString("ClientId");
                if (!string.IsNullOrEmpty(sessionClientId))
                {
                    _logger.LogInformation($"Retrieved client ID from session: {sessionClientId}");
                    return sessionClientId;
                }

                _logger.LogInformation("Starting GetClientId method");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.GetAsync($"{API_BASE_URL}/me");
                _logger.LogInformation($"ME endpoint response status: {response.StatusCode}");

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"ME endpoint response content: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error response from ME endpoint: {response.StatusCode}");
                    return null;
                }

                var userInfo = JsonSerializer.Deserialize<UserInfoResponse>(content,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (userInfo?.Status == "SUCCESS" && userInfo.Data?.Clients != null)
                {
                    var flatworldClient = userInfo.Data.Clients
                        .FirstOrDefault(c => c.Client_Name == "Flatworld Solutions");

                    if (flatworldClient != null)
                    {
                        _logger.LogInformation($"Found Flatworld client ID: {flatworldClient.Client_Id}");

                        // Store in session
                        _httpContextAccessor.HttpContext?.Session.SetString("ClientId", flatworldClient.Client_Id);

                        return flatworldClient.Client_Id;
                    }

                    _logger.LogWarning("Flatworld Solutions client not found in user's clients");
                }
                else
                {
                    _logger.LogWarning($"Invalid response status or no clients data. Status: {userInfo?.Status}");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetClientId: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<bool> RefreshTokenIfNeeded()
        {
            try
            {
                var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("AccessToken");
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("No access token found in session");
                    return false;
                }

                if (IsTokenExpired(accessToken))
                {
                    _logger.LogInformation("Token is expired or expiring soon, refreshing...");
                    return await RefreshToken();
                }

                _logger.LogInformation("Token is still valid");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RefreshTokenIfNeeded: {ex.Message}");
                return false;
            }
        }

        private bool IsTokenExpired(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var expirationTime = jwtToken.ValidTo;
                var timeUntilExpiration = expirationTime - DateTime.UtcNow;

                _logger.LogInformation($"Token expires in: {timeUntilExpiration.TotalMinutes} minutes");

                return timeUntilExpiration.TotalMinutes <= 5;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking token expiration: {ex.Message}");
                return true;
            }
        }

        private async Task<bool> RefreshToken()
        {
            try
            {
                var refreshToken = _httpContextAccessor.HttpContext?.Session.GetString("RefreshToken");
                if (string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("No refresh token found in session");
                    return false;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var payload = new { refresh_token = refreshToken };
                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{API_BASE_URL}/azure/refresh-token",
                    content);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Refresh token response: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent);

                    if (tokenResponse?.Status == "SUCCESS" && tokenResponse.Data != null)
                    {
                        var context = _httpContextAccessor.HttpContext;
                        context?.Session.SetString("AccessToken", tokenResponse.Data.Access_Token);
                        context?.Session.SetString("RefreshToken", tokenResponse.Data.Refresh_Token);

                        var clientId = await GetClientId(tokenResponse.Data.Access_Token);
                        if (!string.IsNullOrEmpty(clientId))
                        {
                            context?.Session.SetString("ClientId", clientId);
                            _logger.LogInformation("Successfully refreshed tokens and updated client ID");
                            return true;
                        }
                    }
                }

                _logger.LogWarning("Failed to refresh token");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RefreshToken: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<string> GetUserEmail(string accessToken)
        {
            try
            {
                // First check session
                var sessionEmail = _httpContextAccessor.HttpContext?.Session.GetString("UserEmail");
                if (!string.IsNullOrEmpty(sessionEmail))
                {
                    _logger.LogInformation($"Retrieved email from session: {sessionEmail}");
                    return sessionEmail;
                }

                _logger.LogInformation("Starting GetUserEmail method");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.GetAsync($"{API_BASE_URL}/me");
                _logger.LogInformation($"ME endpoint response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error response from ME endpoint: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"ME endpoint response content: {content}");

                var userInfo = JsonSerializer.Deserialize<UserInfoResponse>(content,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (userInfo?.Status == "SUCCESS" && userInfo.Data != null)
                {
                    var email = userInfo.Data.Email;
                    _logger.LogInformation($"Found user email: {email}");

                    // Store in session
                    _httpContextAccessor.HttpContext?.Session.SetString("UserEmail", email);

                    return email;
                }

                _logger.LogWarning($"Invalid response status or no user data. Status: {userInfo?.Status}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user email: {ex.Message}");
                return null;
            }
        }
    }
}