using SimpleMail.Web.Errors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Web;

namespace SimpleMail.Web.Extensions
{
    /// <summary>
    /// Provides extended functionality around C# enumerations.
    /// </summary>
    internal static class EnumExtensions
    {
        /// <summary>
        /// Extracts XML docstrings decorating c# enums to provide a brief description.
        /// This is used as a means for controlled message passing between back-end and caller.
        /// </summary>
        /// <param name="en">Enumeration value to describe</param>
        /// <returns>Desrcription of the enumeration value</returns>
        internal static string GetDescription(this Enum en)
        {
            var type = en.GetType();
            var memInfo = type.GetMember(en.ToString());

            if (memInfo != null && memInfo.Length > 0)
            {
                object[] attrs = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attrs != null && attrs.Length > 0)
                {
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }

            return en.ToString();
        }

        /// <summary>
        /// Converts formatting errors to Http Status Codes
        /// </summary>
        internal static HttpStatusCode ToHttpStatusCode(this FormatErrorCode opcode)
        {
            switch (opcode)
            {
                // in case of malformed email
                // return 400: Bad Request.
                case FormatErrorCode.SenderEmailFormatException:
                case FormatErrorCode.EmailMessageEmpty:
                case FormatErrorCode.ToRecipientsEmpty:
                    return HttpStatusCode.BadRequest;

                case FormatErrorCode.EmailSent:
                    return HttpStatusCode.Created;

                // unknown error.
                default:
                    return HttpStatusCode.InternalServerError;
            }
        }
    }
}