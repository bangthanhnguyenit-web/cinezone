using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public class AdminBaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var admin = context.HttpContext.Session.GetString("Admin");

        if (admin == null)
        {
            context.Result = new RedirectResult("/Admin/Login");
        }

        base.OnActionExecuting(context);
    }
}