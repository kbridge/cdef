﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dia2Lib;
using PdbReader.DiaExtra;
using PdbReader.Types;
using PdbReader.Collect;

namespace PdbReader
{
    class Translator
    {
        private bool _translatingTree;
        public CType Translate(IDiaSymbol sym)
        {
            switch ((SymTagEnum)sym.symTag)
            {
                case SymTagEnum.SymTagBaseType:
                    return TranslateBaseType(sym);

                case SymTagEnum.SymTagPointerType:
                    return TranslatePtr(sym);

                case SymTagEnum.SymTagArrayType:
                    return TranslateArr(sym);

                case SymTagEnum.SymTagFunctionType:
                    return TranslateFunc(sym);
                
                case SymTagEnum.SymTagUDT:
                    return IsUnnamed(sym)
                        ? TranslateUnnamedUdt(sym)
                        : TranslateUDT(sym);

                default:
                    return new CPrim("NotImpl");
            }
        }
        private bool IsUnnamed(IDiaSymbol sym)
        {
            return sym.name.Contains('<');
        }
        private CTerm WithAttr(CTerm type, IDiaSymbol sym)
        {
            SortedSet<TypeAttr> attrs = new SortedSet<TypeAttr>();
            if (sym.constType == 1) { attrs.Add(TypeAttrs.Const); }
            if (sym.volatileType == 1) { attrs.Add(TypeAttrs.Volatile); }
            if (sym.unalignedType == 1) { attrs.Add(TypeAttrs.Unaligned); }

            return attrs.Any() ? new CAttrTerm(type, attrs) : type;
        }
        public CPrim _TranslateBaseType(IDiaSymbol sym)
        {
            int size = (int)sym.length;
            switch ((BaseTypeEnum)sym.baseType)
            {
                case BaseTypeEnum.btVoid:
                    return PrimTypes.VOID;

                case BaseTypeEnum.btChar:
                    return PrimTypes.CHAR;
                
                // no handler for btWChar
                // `wchar_t' will be compiled to ULONG

                case BaseTypeEnum.btInt:
                    return IntTypePairs.SelectBySize(size).Signed;
                case BaseTypeEnum.btUInt:
                    return IntTypePairs.SelectBySize(size).Unsigned;

                // the design logic of Dia2Lib is
                // eh.. MS guys must be brain fucked at that time.
                case BaseTypeEnum.btLong:
                    return PrimTypes.LONG;
                case BaseTypeEnum.btULong:
                    return PrimTypes.ULONG;

                case BaseTypeEnum.btFloat:
                    return sym.length == 4 ? PrimTypes.FLOAT : PrimTypes.FLOAT;

                default:
                    return new CPrim("NotImpl_BaseType");
            }
        }
        public CTerm TranslateBaseType(IDiaSymbol sym)
        {
            return WithAttr(_TranslateBaseType(sym), sym);
        }
        public CPtr TranslatePtr(IDiaSymbol sym)
        {
            CType next = Translate(sym.type);
            return new CPtr(next);
        }
        public CArr TranslateArr(IDiaSymbol sym)
        {
            CType next = Translate(sym.type);
            int len = (int)sym.count;           // it should be safe
            return new CArr(next, len);
        }
        public CFunc TranslateFunc(IDiaSymbol sym)
        {
            CType retType = Translate(sym.type);
            CFunc res = new CFunc(retType);

            IDiaEnumSymbols syms;
            sym.findChildren(SymTagEnum.SymTagFunctionArgType, null, 0, out syms);

            foreach (IDiaSymbol argSym in syms)
            {
                CType argType = Translate(argSym.type);
                res.Add(argType);
            }
            return res;
        }
        public CBits TranslateBitField(IDiaSymbol sym)
        {
            return new CBits(TranslateBaseType(sym.type), (int)sym.length);
        }
        private bool IsBitField(IDiaSymbol sym)
        {
            return (LocationTypeEnum)sym.locationType == LocationTypeEnum.LocIsBitField;
        }
        public CType TranslateMember(IDiaSymbol subSym)
        {
            return IsBitField(subSym)
                ? TranslateBitField(subSym)
                : Translate(subSym.type);
        }
        public CStruct TranslateStruct(IDiaSymbol sym)
        {
            IDiaEnumSymbols symbols;
            sym.findChildren(SymTagEnum.SymTagData, null, 0, out symbols);

            CStruct res = new CStruct();
            Offset lastOffset = Offset.Neg;
            foreach (IDiaSymbol subSym in symbols)
            {
                Offset thisOffset = Offset.FromDiaSymbol(subSym);
                if (thisOffset.IsLessThanOrEqualTo(lastOffset))
                {
                    symbols.Reset();
                    return TranslateStruct2(symbols);
                }

                string name = subSym.name;
                CType type = TranslateMember(subSym);
                res.Add(type, name);

                lastOffset = thisOffset;
            }

            return res;
        }
        public CStruct TranslateStruct2(IDiaEnumSymbols symbols)
        {
            return new Collector(this).CollectStruct(symbols);
        }
        public CUnion TranslateUnion(IDiaSymbol sym)
        {
            IDiaEnumSymbols symbols;
            sym.findChildren(SymTagEnum.SymTagData, null, 0, out symbols);

            CUnion res = new CUnion();
            foreach (IDiaSymbol subSym in symbols)
            {
                Offset thisOffset = Offset.FromDiaSymbol(subSym);
                if (!thisOffset.IsEqualTo(Offset.Zero))
                {
                    symbols.Reset();
                    return TranslateUnion2(symbols);
                }

                string name = subSym.name;
                CType type = TranslateMember(subSym);
                res.Add(type, name);
            }
            return res;
        }
        public CUnion TranslateUnion2(IDiaEnumSymbols symbols)
        {
            return new Collector(this).CollectUnion(symbols);
        }
        private string InternName(string name)
        {
            if (name.StartsWith("_"))
            {
                return name.Substring(1);
            }
            else
            {
                return name;
            }
        }
        public CType TranslateUDT(IDiaSymbol sym)
        {
            return new CTypeRef(InternName(sym.name));
        }
        public CType TranslateUnnamedUdt(IDiaSymbol sym)
        {
            switch ((UdtKindEnum)sym.udtKind)
            {
                case UdtKindEnum.UdtStruct:
                    return TranslateStruct(sym);
                case UdtKindEnum.UdtUnion:
                    return TranslateUnion(sym);
                default:
                    return new CPrim("NotImpl_Udt");
            }
        }
    }
}