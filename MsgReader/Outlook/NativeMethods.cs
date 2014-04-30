﻿using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

namespace DocumentServices.Modules.Readers.MsgReader.Outlook
{
    public partial class Storage
    {
        /// <summary>
        /// Contains all the used native methods
        /// </summary>
        public static class NativeMethods
        {
            #region Stgm enum
            [Flags]
            public enum Stgm
            {
                // ReSharper disable InconsistentNaming
                STGM_DIRECT = 0,
                STGM_FAILIFTHERE = 0,
                STGM_READ = 0,
                STGM_WRITE = 1,
                STGM_READWRITE = 2,
                STGM_SHARE_EXCLUSIVE = 0x10,
                STGM_SHARE_DENY_WRITE = 0x20,
                STGM_SHARE_DENY_READ = 0x30,
                STGM_SHARE_DENY_NONE = 0x40,
                STGM_CREATE = 0x1000,
                STGM_TRANSACTED = 0x10000,
                STGM_CONVERT = 0x20000,
                STGM_PRIORITY = 0x40000,
                STGM_NOSCRATCH = 0x100000,
                STGM_NOSNAPSHOT = 0x200000,
                STGM_DIRECT_SWMR = 0x400000,
                STGM_DELETEONRELEASE = 0x4000000,
                STGM_SIMPLE = 0x8000000,
                // ReSharper restore InconsistentNaming
            }
            #endregion

            #region DllImports
            [DllImport("ole32.DLL")]
            internal static extern int CreateILockBytesOnHGlobal(IntPtr hGlobal, bool fDeleteOnRelease, out ILockBytes ppLkbyt);

            [DllImport("ole32.DLL")]
            internal static extern int StgIsStorageILockBytes(ILockBytes plkbyt);

            [DllImport("ole32.DLL")]
            internal static extern int StgCreateDocfileOnILockBytes(ILockBytes plkbyt, Stgm grfMode, uint reserved,
                out IStorage ppstgOpen);

            [DllImport("ole32.DLL")]
            internal static extern void StgOpenStorageOnILockBytes(ILockBytes plkbyt, IStorage pstgPriority, Stgm grfMode,
                IntPtr snbExclude, uint reserved,
                out IStorage ppstgOpen);

            [DllImport("ole32.DLL")]
            internal static extern int StgIsStorageFile([MarshalAs(UnmanagedType.LPWStr)] string wcsName);

            [DllImport("ole32.DLL")]
            internal static extern int StgOpenStorage([MarshalAs(UnmanagedType.LPWStr)] string wcsName,
                IStorage pstgPriority, Stgm grfMode, IntPtr snbExclude, int reserved,
                out IStorage ppstgOpen);
            #endregion

            #region CloneStorage
            internal static IStorage CloneStorage(IStorage source, bool closeSource)
            {
                IStorage memoryStorage = null;
                ILockBytes memoryStorageBytes = null;
                try
                {
                    //create a ILockBytes (unmanaged byte array) and then create a IStorage using the byte array as a backing store
                    CreateILockBytesOnHGlobal(IntPtr.Zero, true, out memoryStorageBytes);
                    StgCreateDocfileOnILockBytes(memoryStorageBytes, Stgm.STGM_CREATE | Stgm.STGM_READWRITE | Stgm.STGM_SHARE_EXCLUSIVE,
                        0, out memoryStorage);

                    //copy the source storage into the new storage
                    source.CopyTo(0, null, IntPtr.Zero, memoryStorage);
                    memoryStorageBytes.Flush();
                    memoryStorage.Commit(0);

                    // Ensure memory is released
                    ReferenceManager.AddItem(memoryStorage);
                }
                catch
                {
                    if (memoryStorage != null)
                        Marshal.ReleaseComObject(memoryStorage);
                }
                finally
                {
                    if (memoryStorageBytes != null)
                        Marshal.ReleaseComObject(memoryStorageBytes);

                    if (closeSource)
                        Marshal.ReleaseComObject(source);
                }

                return memoryStorage;
            }
            #endregion

            #region IEnumSTATSTG
            [ComImport, Guid("0000000D-0000-0000-C000-000000000046"),
             InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IEnumSTATSTG
            {
                void Next(uint celt, [MarshalAs(UnmanagedType.LPArray), Out] STATSTG[] rgelt, out uint pceltFetched);
                void Skip(uint celt);
                void Reset();

                [return: MarshalAs(UnmanagedType.Interface)]
                IEnumSTATSTG Clone();
            }
            #endregion

            #region ILockBytes
            [ComImport, Guid("0000000A-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface ILockBytes
            {
                void ReadAt([In, MarshalAs(UnmanagedType.U8)] long ulOffset,
                    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pv,
                    [In, MarshalAs(UnmanagedType.U4)] int cb,
                    [Out, MarshalAs(UnmanagedType.LPArray)] int[] pcbRead);

                void WriteAt([In, MarshalAs(UnmanagedType.U8)] long ulOffset,
                    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pv,
                    [In, MarshalAs(UnmanagedType.U4)] int cb,
                    [Out, MarshalAs(UnmanagedType.LPArray)] int[] pcbWritten);

                void Flush();
                void SetSize([In, MarshalAs(UnmanagedType.U8)] long cb);

                void LockRegion([In, MarshalAs(UnmanagedType.U8)] long libOffset,
                    [In, MarshalAs(UnmanagedType.U8)] long cb,
                    [In, MarshalAs(UnmanagedType.U4)] int dwLockType);

                void UnlockRegion([In, MarshalAs(UnmanagedType.U8)] long libOffset,
                    [In, MarshalAs(UnmanagedType.U8)] long cb,
                    [In, MarshalAs(UnmanagedType.U4)] int dwLockType);

                void Stat([Out] out STATSTG pstatstg, [In, MarshalAs(UnmanagedType.U4)] int grfStatFlag);
            }
            #endregion

            #region IStorage
            [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000000B-0000-0000-C000-000000000046")]
            public interface IStorage
            {
                [return: MarshalAs(UnmanagedType.Interface)]
                IStream CreateStream([In, MarshalAs(UnmanagedType.BStr)] string pwcsName,
                    [In, MarshalAs(UnmanagedType.U4)] Stgm grfMode,
                    [In, MarshalAs(UnmanagedType.U4)] int reserved1,
                    [In, MarshalAs(UnmanagedType.U4)] int reserved2);

                [return: MarshalAs(UnmanagedType.Interface)]
                IStream OpenStream([In, MarshalAs(UnmanagedType.BStr)] string pwcsName, IntPtr reserved1,
                    [In, MarshalAs(UnmanagedType.U4)] Stgm grfMode,
                    [In, MarshalAs(UnmanagedType.U4)] int reserved2);

                [return: MarshalAs(UnmanagedType.Interface)]
                IStorage CreateStorage([In, MarshalAs(UnmanagedType.BStr)] string pwcsName,
                    [In, MarshalAs(UnmanagedType.U4)] Stgm grfMode,
                    [In, MarshalAs(UnmanagedType.U4)] int reserved1,
                    [In, MarshalAs(UnmanagedType.U4)] int reserved2);

                [return: MarshalAs(UnmanagedType.Interface)]
                IStorage OpenStorage([In, MarshalAs(UnmanagedType.BStr)] string pwcsName, IntPtr pstgPriority,
                    [In, MarshalAs(UnmanagedType.U4)] Stgm grfMode, IntPtr snbExclude,
                    [In, MarshalAs(UnmanagedType.U4)] int reserved);

                void CopyTo(int ciidExclude, [In, MarshalAs(UnmanagedType.LPArray)] Guid[] pIidExclude,
                    IntPtr snbExclude, [In, MarshalAs(UnmanagedType.Interface)] IStorage stgDest);

                void MoveElementTo([In, MarshalAs(UnmanagedType.BStr)] string pwcsName,
                    [In, MarshalAs(UnmanagedType.Interface)] IStorage stgDest,
                    [In, MarshalAs(UnmanagedType.BStr)] string pwcsNewName,
                    [In, MarshalAs(UnmanagedType.U4)] int grfFlags);

                void Commit(int grfCommitFlags);
                void Revert();

                void EnumElements([In, MarshalAs(UnmanagedType.U4)] int reserved1, IntPtr reserved2,
                    [In, MarshalAs(UnmanagedType.U4)] int reserved3,
                    [MarshalAs(UnmanagedType.Interface)] out IEnumSTATSTG ppVal);

                void DestroyElement([In, MarshalAs(UnmanagedType.BStr)] string pwcsName);

                void RenameElement([In, MarshalAs(UnmanagedType.BStr)] string pwcsOldName,
                    [In, MarshalAs(UnmanagedType.BStr)] string pwcsNewName);

                void SetElementTimes([In, MarshalAs(UnmanagedType.BStr)] string pwcsName, [In] FILETIME pctime,
                    [In] FILETIME patime, [In] FILETIME pmtime);

                void SetClass([In] ref Guid clsid);
                void SetStateBits(int grfStateBits, int grfMask);
                void Stat([Out] out STATSTG pStatStg, int grfStatFlag);
            }
            #endregion
        }
    }
}
