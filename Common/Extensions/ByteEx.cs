using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class ByteEx
    {
        static readonly IFastCopier _copier;

        static AssemblyName _asmName = new AssemblyName() { Name = "FastCopier" };
        static ModuleBuilder _modBuilder;
        static AssemblyBuilder _asmBuilder;

        static ByteEx()
        {
            _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(_asmName, AssemblyBuilderAccess.Run);
            _modBuilder = _asmBuilder.DefineDynamicModule(_asmName.Name, _asmName.Name + ".dll", false);

            var typeBuilder = _modBuilder.DefineType("FastCopier",
                       TypeAttributes.Public
                       | TypeAttributes.AutoClass
                       | TypeAttributes.AnsiClass
                       | TypeAttributes.Class
                       | TypeAttributes.Serializable
                       | TypeAttributes.BeforeFieldInit);
            typeBuilder.AddInterfaceImplementation(typeof(IFastCopier));
            var copyMethod = typeBuilder.DefineMethod("Copy",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(void),
                new Type[] { typeof(byte[]), typeof(byte[]), typeof(int), typeof(uint) });
            var code = copyMethod.GetILGenerator();

            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Ldelema, typeof(byte));
            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Ldarg_3);
            code.Emit(OpCodes.Ldelema, typeof(byte));
            code.Emit(OpCodes.Ldarg, 4);
            code.Emit(OpCodes.Cpblk);
            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(copyMethod, typeof(IFastCopier).GetMethod("Copy"));

            var copierType = typeBuilder.CreateType();
            _copier = (IFastCopier)Activator.CreateInstance(copierType);
        }


        public static void Copy(this byte[] src, int srcOffset, byte[] dst,  uint count)
        {
            if (src == null || srcOffset < 0 ||
                dst == null || count < 0 || srcOffset > src.Length 
                || count > dst.Length || count > src.Length)
            {
                throw new System.ArgumentException();
            }

            _copier.Copy(src, dst, srcOffset, count);
        }

        public static string ToBase16(this byte[] bytes)
        {
            return ToBase16(bytes, -1, -1);
        }

        public static string ToBase16(this byte[] bytes, int maxLength)
        {
            return ToBase16(bytes, maxLength, -1);
        }

        /// <summary>
        /// Coverts the provided byte array to a base16 string of the given max length.  If the converted string exceeds maxLength in length,
        /// the remain characters are truncated from the returned string.  Specify -1 to avoid truncation.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string ToBase16(this byte[] bytes, int maxLength, int dashPosition)
        {
            string hash = Convert.ToBase64String(bytes);
            return hash.ToBase16(maxLength, dashPosition);
        }
    }

    public interface IFastCopier
    {
        void Copy(byte[] source, byte[] dest, int offset, uint count);
    }
}
