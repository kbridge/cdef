﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PdbReader.Types
{
    abstract class CType
    {
        protected static string MaybeSpace(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            else
            {
                return " " + s;
            }
        }

        public string Wrap(string var, out CType core)
        {
            string s = var;
            CType t = this;
            CWrap w;
            while ((w = t as CWrap) != null)
            {
                s = w.Decorate(s);
                t = w.Next;
            }
            core = t;
            return s;
        }
        public string Define(string var, string indent, string tab)
        {
            CType core1;
            CTerm core2;
            string s1, s2;

            s1 = Wrap(var, out core1);
            core2 = (CTerm)core1;

            s2 = indent + core2.PartDef(indent, tab);
            return s2 + MaybeSpace(s1) + ";\n";
        }
        private string TryGetPrefix(CType t)
        {
            string s;
            try
            {
                if (t is CAttrTerm)
                {
                    CAttrTerm t2 = (CAttrTerm)t;
                    s = t2.AttrStr + " " + ((CPrefix)t2.CoreType).Prefix;
                }
                else
                {
                    s = ((CPrefix)t).Prefix;
                }
            }
            catch (InvalidCastException)
            {
                throw new InvalidOperationException();
            }
            return s;
        }
        public string Sig
        {
            get
            {
                CType core;
                string s1, s2;

                s1 = Wrap("", out core);
                s2 = TryGetPrefix(core);
                return s2 + MaybeSpace(s1);
            }
        }
    }
}