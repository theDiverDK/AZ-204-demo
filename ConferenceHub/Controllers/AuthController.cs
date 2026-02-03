using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceHub.Controllers;

public class AuthController : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignIn(string? returnUrl = "/")
    {
        var redirectUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    // Accept both GET and POST to avoid 405 from UI/link variations during demos.
    [Authorize]
    [AcceptVerbs("GET", "POST")]
    [ActionName("SignOut")]
    public IActionResult SignOutUser()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = Url.Action("Index", "Home") },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
