using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Data;
using InternLink.Web.Helpers;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Services.Email;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly IAppEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ApplicationDbContext db,
        IAppEmailSender emailSender,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Register

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // User row + linked profile row must both commit or both roll back.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await transaction.RollbackAsync(ct);
                return View(model);
            }

            if (model.Role == RegistrationRole.Student)
            {
                await _userManager.AddToRoleAsync(user, "Student");
                _db.Students.Add(new Student
                {
                    UserId = user.Id,
                    FirstName = model.FirstName!.Trim(),
                    LastName = model.LastName!.Trim(),
                    InstitutionalId = model.InstitutionalId!.Trim(),
                    Department = model.Department!.Trim(),
                    CGPA = model.CGPA!.Value,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Company");
                _db.Companies.Add(new Company
                {
                    UserId = user.Id,
                    CompanyName = model.CompanyName!.Trim(),
                    IndustrySector = model.IndustrySector!.Trim(),
                    CorporateWebsite = string.IsNullOrWhiteSpace(model.CorporateWebsite) ? null : model.CorporateWebsite.Trim(),
                    VerificationStatus = VerificationStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (DbExceptionMapper.IsUniqueConstraintViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(ex, "Registration failed on a unique constraint for {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "An account with these details already exists.");
            return View(model);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Registration failed unexpectedly for {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "Registration could not be completed. Please try again.");
            return View(model);
        }

        // Email confirmation is required before sign-in (SignIn.RequireConfirmedEmail = true).
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, token },
            Request.Scheme);

        await _emailSender.SendEmailAsync(
            user.Email!,
            "Confirm your InternLink account",
            $"Please confirm your account by clicking this link: <a href=\"{confirmationLink}\">Confirm email</a>",
            ct);

        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    public IActionResult RegisterConfirmation()
    {
        return View();
    }

    // ------------------------------------------------------------------ Confirm email

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string token)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return View(false);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return View(false);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return View(result.Succeeded);
    }

    // ------------------------------------------------------------------ Login

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Suspension is checked before anything else.
        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account has been suspended. Please contact an administrator.");
            return View(model);
        }

        // Password check only — OTP second factor and SignInAsync happen after (see below).
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "This account is locked due to multiple failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "You must confirm your email address before signing in.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // [OTP step placeholder] — Prompt 8 inserts the email OTP second factor here, between
        // the password check and SignInAsync. Do not collapse this back into PasswordSignInAsync.

        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);
        return RedirectToLocal(model.ReturnUrl);
    }

    // ------------------------------------------------------------------ Logout

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ------------------------------------------------------------------ Access denied

    [HttpGet]
    public IActionResult Denied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
