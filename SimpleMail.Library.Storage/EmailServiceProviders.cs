using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleMail.Library.Storage
{
    public enum EmailServiceProviders
    {
        None = 1,

        /// <summary>
        /// Use Amazon's Simple Email Service
        /// </summary>
        Amazon = 2,

        // Use SendGrid
        SendGrid = 3
    }
}