using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace SimpleMail.Web.Errors
{
    /// <summary>
    /// Encapsulates errors related to malformed emails.
    /// </summary>
    public enum FormatErrorCode
    {
        /// <summary>
        /// We usually ignore the 0 value <see>
        ///         <cref>http://goo.gl/JOeTRq</cref>
        ///     </see>
        /// </summary>
        None = 0,

        /// <summary>
        /// The email was sent successfully.
        /// </summary>
        [Description("Email sent successfully.")]
        EmailSent = 1,

        /// <summary>
        /// The sender email address was not valid.
        /// </summary>
        [Description("The sender email address was not valid.")]
        SenderEmailFormatException = 2,

        /// <summary>
        /// The email body cannot be empty
        /// </summary>
        [Description("The email body cannot be empty.")]
        EmailMessageEmpty = 3,

        /// <summary>
        /// The 'To' field in the email cannot be empty.
        /// There must be at least one sender
        /// </summary>
        [Description("The 'To' field in the email cannot be empty.")]
        ToRecipientsEmpty = 4,
    }
}