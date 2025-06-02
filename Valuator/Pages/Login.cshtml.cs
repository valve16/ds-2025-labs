using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;

namespace Valuator.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IDatabase _db;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(ILogger<LoginModel> logger, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _db = redis.GetDatabase();
        }

        [BindProperty]
        public string? Username { get; set; }
        [BindProperty]
        public string? Password { get; set; }

        public void OnGet()
        {
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError(string.Empty, "Требуется имя пользователя и пароль");
                return Page();
            }
            string userKey = "USER-" + Username;

            string? storedUser = _db.StringGet(userKey);

            // Проверяем существование пользователя
            if (string.IsNullOrEmpty(storedUser))
            {
                ModelState.AddModelError(string.Empty, "Неверное имя пользователя или пароль");
                return Page();
            }

            // Хешируем введенный пароль и сравниваем с сохраненным хешем
            string inputHash = HashPassword(Password);

            if (inputHash != storedUser)
            {
                ModelState.AddModelError(string.Empty, "Неверное имя пользователя или пароль");
                return Page();
            }

            // Создаем куки аутентификации
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, Username),
            };

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true // Куки будут сохраняться после закрытия браузера
            };


            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
            return RedirectToPage("/Index");
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }

}
