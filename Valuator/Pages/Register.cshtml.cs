using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Valuator.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly IDatabase _db;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(ILogger<RegisterModel> logger, IConnectionMultiplexer redis)
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

            string userKey = "USER-" + Username;

            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError(string.Empty, "Username and password are required.");
                return Page();
            }

            if (_db.StringGet(userKey).HasValue)
            {
                ModelState.AddModelError(string.Empty, "Имя уже существует");
                return Page();
            }

            // Хешируем пароль
            string hashedPassword = HashPassword(Password);

            // Сохраняем в Redis: ключ - имя пользователя, значение - хеш пароля
            _db.StringSet(userKey, hashedPassword);

            // Создаем куки для аутентификации
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, Username),
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

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
