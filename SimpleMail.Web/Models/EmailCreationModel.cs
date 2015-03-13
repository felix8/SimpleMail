using SimpleMail.Web.Errors;
using SimpleMail.Web.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace SimpleMail.Web.Models
{
    /// <summary>
    /// Represents the email body (except attachments)
    /// </summary>
    public class EmailCreationModel
    {
        [Required]
        [Display(Name = "To Recipients")]
        [DataType(DataType.EmailAddress)]
        public string ToRecipientEmailIds { get; set; }

        [Display(Name = "Cc Recipients")]
        [DataType(DataType.EmailAddress)]
        public string CcRecipientEmailIds { get; set; }

        [Display(Name = "Bcc Recipients")]
        [DataType(DataType.EmailAddress)]
        public string BccRecipientEmailIds { get; set; }

        [Display(Name = "Subject")]
        [StringLength(300, ErrorMessage = "Subject should be at least {2} characters long.", MinimumLength = 2)]
        [DataType(DataType.Text)]
        public string Subject { get; set; }

        [Required]
        [Display(Name = "Email body.")]
        [DataType(DataType.Html)]
        public string Message { get; set; }

        internal bool Verify(out EmailErrorModel result)
        {
            if (this.Message == null)
            {
                result = new EmailErrorModel
                {
                    ErrorCode = (int)FormatErrorCode.EmailMessageEmpty.ToHttpStatusCode(),
                    Message = EnumExtensions.GetDescription(FormatErrorCode.EmailMessageEmpty)
                };
                return false;
            }
            if ((this.ToRecipientEmailIds == null) || (this.ToRecipientEmailIds == string.Empty))
            {
                result = new EmailErrorModel
                {
                    ErrorCode = (int)FormatErrorCode.ToRecipientsEmpty.ToHttpStatusCode(),
                    Message = EnumExtensions.GetDescription(FormatErrorCode.ToRecipientsEmpty)
                };
                return false;
            }
            result = null;
            return true;
        }
    }
}