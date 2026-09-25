using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PlataformaCreditos.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;

    public IndexModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public string Username { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"No se pudo cargar el usuario.");
        }

        Username = await _userManager.GetUserNameAsync(user) ?? string.Empty;
        return Page();
    }
}
