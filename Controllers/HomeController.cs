using Botatwork_in_Livechat.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json;
using Botatwork_in_Livechat.Services;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;

namespace Botatwork_in_Livechat.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ITokenService _tokenService;
        private readonly IChatStorageService _chatStorage;

        public HomeController(ILogger<HomeController> logger, ITokenService tokenService, IChatStorageService chatStorage)
        {
            _logger = logger;
            _tokenService = tokenService;
            _chatStorage = chatStorage;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    var userEmail = HttpContext.Session.GetString("UserEmail") ??
                                   User.FindFirst(ClaimTypes.Email)?.Value;

                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        await _tokenService.RefreshTokenIfNeeded();
                        return RedirectToAction("Dashboard");
                    }
                }
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in Index: {ex.Message}");
                return View();
            }
        }

        public async Task<IActionResult> Callback(string nonce, string response)
        {
            try
            {
                if (string.IsNullOrEmpty(response))
                {
                    TempData["Error"] = "No response received.";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation($"Raw Response: {response}");
                string decodedResponse = System.Web.HttpUtility.UrlDecode(response);
                _logger.LogInformation($"Decoded Response: {decodedResponse}");

                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
                    decodedResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (loginResponse?.Status == "SUCCESS" && loginResponse.Data != null)
                {
                    var accessToken = loginResponse.Data.Access_Token;
                    var refreshToken = loginResponse.Data.Refresh_Token;

                    // Get user info
                    var clientId = await _tokenService.GetClientId(accessToken);
                    var userEmail = await _tokenService.GetUserEmail(accessToken);

                    _logger.LogInformation($"Retrieved user email: {userEmail}");

                    if (string.IsNullOrEmpty(clientId))
                    {
                        TempData["Error"] = "Access denied. User not associated with Flatworld Solutions.";
                        return RedirectToAction("Index");
                    }

                    if (string.IsNullOrEmpty(userEmail))
                    {
                        TempData["Error"] = "Unable to retrieve user email.";
                        return RedirectToAction("Index");
                    }

                    // Store in session
                    HttpContext.Session.SetString("AccessToken", accessToken);
                    HttpContext.Session.SetString("RefreshToken", refreshToken);
                    HttpContext.Session.SetString("ClientId", clientId);
                    HttpContext.Session.SetString("UserEmail", userEmail);

                    // Create claims
                    var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, userEmail),
                    new Claim(ClaimTypes.Email, userEmail),
                    new Claim("AccessToken", accessToken),
                    new Claim("ClientId", clientId)
                };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
                        AllowRefresh = true
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties
                    );

                    return RedirectToAction("Dashboard");
                }

                TempData["Error"] = $"Login failed: Status = {loginResponse?.Status ?? "null"}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in Callback: {ex.Message}");
                TempData["Error"] = "Error processing login response";
                return RedirectToAction("Index");
            }
        }


        

        

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                await _tokenService.RefreshTokenIfNeeded();

                var userEmail = HttpContext.Session.GetString("UserEmail");
                var accessToken = HttpContext.Session.GetString("AccessToken");

                // If email is missing, try multiple sources
                if (string.IsNullOrEmpty(userEmail))
                {
                    userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

                    if (string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(accessToken))
                    {
                        userEmail = await _tokenService.GetUserEmail(accessToken);
                    }

                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        HttpContext.Session.SetString("UserEmail", userEmail);
                    }
                    else
                    {
                        _logger.LogWarning("User email not found in any source");
                        return RedirectToAction("Index");
                    }
                }

                ViewBag.UserEmail = userEmail;
                ViewBag.ClientId = HttpContext.Session.GetString("ClientId");

                _logger.LogInformation($"Dashboard accessed by user: {userEmail}");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in Dashboard: {ex.Message}");
                return RedirectToAction("Index");
            }
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> StoreMessage([FromBody] StoreMessageRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Message data is required");
                }

                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return BadRequest("User email not found");
                }

                await _chatStorage.StoreMessage(
                    request.ChatId,
                    request.VisitorMessage,
                    request.BotResponse,
                    userEmail
                );

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error storing message: {ex.Message}");
                return StatusCode(500, "Error storing message");
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UpdateCopyStatus([FromBody] UpdateCopyStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.MessageHash))
                {
                    return BadRequest("Message hash is required");
                }

                await _chatStorage.UpdateCopyStatus(request.MessageHash);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating copy status: {ex.Message}");
                return StatusCode(500, "Error updating copy status");
            }
        }

        // Add these classes at the bottom of your file or in separate files
        public class UpdateCopyStatusRequest
        {
            public string MessageHash { get; set; }
        }

        public class StoreMessageRequest
        {
            public string ChatId { get; set; }
            public string VisitorMessage { get; set; }
            public string BotResponse { get; set; }
        }




        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in Logout: {ex.Message}");
                return RedirectToAction("Index");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}