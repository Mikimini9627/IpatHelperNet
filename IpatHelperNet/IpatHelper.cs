// IpatHelper.cs
using System;
using System.Runtime.InteropServices;

namespace IpatHelperNet
{
    // --- 列挙型 ---

    public enum KAISAI : ushort
    {
        SAPPORO = 0, HAKODATE, FUKUSHIMA, NIIGATA,
        TOKYO, NAKAYAMA, CHUKYO, KYOTO, HANSHIN, KOKURA,
        SONODA, HIMEJI, NAGOYA, MONBETSU, MORIOKA, MIZUSAWA,
        URAWA, FUNABASHI, OI, KAWASAKI, KASAMATSU, KANAZAWA,
        KOCHI, SAGA, LONGCHAMP, SHATIN, SANTAANITA,
        DEAUVILE, CHURCHILLDOWNS, ABDULAZIZ
    }

    public enum HOUSHIKI : byte { NORMAL = 0, FORMATION, BOX }

    public enum SHIKIBETSU : byte
    {
        WIN = 1, PLACE, BRACKETQUINELLA, QUINELLAPLACE,
        QUINELLA, EXACTA, TRIO, TRIFECTA
    }

    [Flags]
    public enum RETURN_VALUE : uint
    {
        SUCCESS = 0b00000001,
        UNSUCCESS = 0b00000010,
        FAILED_CHUOU = 0b00000100,
        FAILED_CHIHOU = 0b00001000,
        FAILED_COMMUNICATE_CHUOU = 0b00010000,
        FAILED_COMMUNICATE_CHIHOU = 0b00100000
    }

    // --- 構造体 ---

    [StructLayout(LayoutKind.Sequential)]
    public struct ST_BET_DATA
    {
        public ushort usPlace;
        public byte ucRaceNo;
        public byte ucYoubi;
        public byte ucHoushiki;
        public byte ucShikibetsu;
        public uint unKingaku;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] unUmaban;       // 馬番ビットフラグ (列3つ分)
        public uint unTotalAmount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ST_BET_DATA_WIN5
    {
        public uint unKingaku;
        public byte ucYoubi;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] unUmaban;       // レース5つ分
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ST_PURCHASE_DATA
    {
        public ushort usRemainBetCount;
        public uint unBalance;
        public uint unDayPurchase;
        public uint unDayPayout;
        public uint unTotalPurchase;
        public uint unTotalPayout;
        public uint unTicketCount;
        public IntPtr pobjTicketData; // ST_TICKET_DATA* (手動マーシャリング)
    }

    // --- P/Invoke宣言 ---

    internal static class NativeMethods
    {
        private const string DllName = "IpatHelper";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern uint Login(string szINetId, string szId, string szPassword, string szPars);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint Logout();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint Deposit(uint unDepositValue, ushort usRetryCount = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint Withdraw(ushort usRetryCount = 10);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint GetPurchaseData(ref ST_PURCHASE_DATA pobjStatus);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleasePurchaseData(ref ST_PURCHASE_DATA pobjStatus);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern uint GetBetInstance(
            ushort usPlace, byte ucRaceNo,
            ushort usYear, byte ucMonth, byte ucDay,
            byte ucHoushiki, byte ucShikibetsu,
            uint unKingaku, string szKaime,
            ref ST_BET_DATA pobjBetData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint Bet(
            [In] ST_BET_DATA[] pobjBetData,
            ushort usBetCount,
            ushort usWaitMilliSeconds = 500);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern uint GetBetInstanceWin5(
            uint unKingaku, ushort usYear, byte ucMonth, byte ucDay,
            string szKaime, ref ST_BET_DATA_WIN5 pobjBetData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint BetWin5(ST_BET_DATA_WIN5 objBetData, ushort usWaitMilliSeconds = 500);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint SetAutoDepositFlag(
            [MarshalAs(UnmanagedType.I1)] bool bEnable,
            uint unDepositValue = 1000,
            ushort usConfirmTimeout = 10000);
    }

    // --- 使いやすいC#ラッパークラス ---

    public sealed class IpatHelper : IDisposable
    {
        private bool _loggedIn = false;

        public RETURN_VALUE Login(string inetId, string id, string password, string pars)
        {
            var ret = (RETURN_VALUE)NativeMethods.Login(inetId, id, password, pars);
            if (ret.HasFlag(RETURN_VALUE.SUCCESS)) _loggedIn = true;
            return ret;
        }

        public RETURN_VALUE Logout()
        {
            _loggedIn = false;
            return (RETURN_VALUE)NativeMethods.Logout();
        }

        public RETURN_VALUE Deposit(uint amount, ushort retryCount = 10)
            => (RETURN_VALUE)NativeMethods.Deposit(amount, retryCount);

        public RETURN_VALUE Withdraw(ushort retryCount = 10)
            => (RETURN_VALUE)NativeMethods.Withdraw(retryCount);

        public RETURN_VALUE GetBetInstance(
            KAISAI place, byte raceNo,
            ushort year, byte month, byte day,
            HOUSHIKI houshiki, SHIKIBETSU shikibetsu,
            uint amount, string kaime,
            out ST_BET_DATA betData)
        {
            betData = new ST_BET_DATA();
            return (RETURN_VALUE)NativeMethods.GetBetInstance(
                (ushort)place, raceNo, year, month, day,
                (byte)houshiki, (byte)shikibetsu,
                amount, kaime, ref betData);
        }

        public RETURN_VALUE Bet(ST_BET_DATA[] betDataArray, ushort waitMs = 500)
            => (RETURN_VALUE)NativeMethods.Bet(betDataArray, (ushort)betDataArray.Length, waitMs);

        public RETURN_VALUE SetAutoDepositFlag(bool enable, uint amount = 1000, ushort timeoutMs = 10000)
            => (RETURN_VALUE)NativeMethods.SetAutoDepositFlag(enable, amount, timeoutMs);

        public void Dispose()
        {
            if (_loggedIn) Logout();
        }
    }
}