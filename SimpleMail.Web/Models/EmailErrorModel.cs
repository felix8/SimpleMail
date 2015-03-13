using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace SimpleMail.Web.Models
{
    /// <summary>
    /// Contains failure information during email processing.
    /// </summary>
    public class EmailErrorModel
    {
        [Display(Name = "Email processing failure notification.")]
        public string Message { get; set; }

        [Display(Name = "Http Error Code.")]
        public int ErrorCode { get; set; }
    }
}