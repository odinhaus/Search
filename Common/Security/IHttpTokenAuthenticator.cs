﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface IHttpTokenAuthenticator
    {
        bool Authenticate(string token, TokenType tokenType, out IPrincipal principal);
    }
}
