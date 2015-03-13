using SimpleMail.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Owin;
using SimpleMail.Library.Storage;
using System.Net;
using System.Diagnostics;

namespace SimpleMail.Web.Controllers
{
    [Authorize]
    public class EmailController : Controller
    {
        private ApplicationUserManager _userManager;

        public EmailController()
        {
        }

        public EmailController(ApplicationUserManager userManager)
        {
            UserManager = userManager;
        }

        /// <summary>
        /// Obtain user context from Microsoft.OWIN (security module)
        /// </summary>
        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        /// <summary>
        /// Landing point for Email page.
        /// Serves a form for email creation.
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            return View(new EmailCreationModel());
        }

        [HttpGet]
        public ActionResult Send()
        {
            return View();
        }

        /// <summary>
        /// Reads email information provider by the caller. After verifying that
        /// the information is correct, it writes the email metadata (sender, to recipients etc.)
        /// to Table Storage. Attachments, if any are uploaded to a Blob Store.
        /// Finally a job is sent to back-end workers through ServiceBus to process the email message.
        /// </summary>
        /// <returns>View containing status of the request</returns>
        [HttpPost]
        public async Task<ActionResult> Send(EmailCreationModel email, IEnumerable<HttpPostedFileBase> attachments)
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            try
            {
                // if email is not valid, return error response
                EmailErrorModel result;
                if (!email.Verify(out result))
                    return View(result);

                // if any attachments are present upload to blob storage
                var collatedUrl = await BlobStorageProvider.Instance.Write(attachments ?? new List<HttpPostedFileBase>());

                var emailInfo = new Dictionary<string, string>
                {
                    {"ToRecipientEmailIds", email.ToRecipientEmailIds},
                    {"CcRecipientEmailIds", email.CcRecipientEmailIds},
                    {"BccRecipientEmailIds", email.BccRecipientEmailIds},
                    {"Subject", email.Subject},
                    {"Message", email.Message}
                };

                // @todo: due to domain issues, currently this is static.
                //var tableReference = await TableStorageProvider.Instance.Write(user.Email, emailInfo, collatedUrl);
                var tableReference = await TableStorageProvider.Instance.Write(Properties.Settings.Default.DomainEmailAccount, emailInfo, collatedUrl);

                if (tableReference == null)
                    return View(new EmailErrorModel
                    {
                        ErrorCode = (int)HttpStatusCode.ServiceUnavailable,
                        Message = "Could not write email to persistant storage. Retry."
                    });

                // send a message to service bus for the email workers
                if (!(await ServiceBusProvider.Instance.Send(tableReference, collatedUrl)))
                    return View(new EmailErrorModel
                    {
                        ErrorCode = (int)HttpStatusCode.ServiceUnavailable,
                        Message = "Could not notify workers to process email. Retry."
                    });
            }
            catch (Exception e)
            {
                Trace.Write(e);

                // we don't want to send the error stack back to the caller, just send StatusCode.
                return View(new EmailErrorModel
                {
                    ErrorCode = (int)HttpStatusCode.InternalServerError,
                    Message = "Unknown error occurred. Please leave a comment at https://github.com/felix8/simplemail/issues"
                });
            }

            // return a success page.
            return View("SendConfirmation");
        }
    }
}