// IpatHelper.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IpatHelperNet
{
    public class IpatHelper
    {
        #region 定数
        /// <summary>フォーメーションでの列数(<see cref="ST_BET_DATA.horseNo"/> の要素数)</summary>
        public const int UMABAN_COLUMN_COUNT = 3;

        /// <summary>購入履歴の列数(<see cref="ST_TICKET_DATA_DETAIL.horseNo"/> の要素数。WIN5 の 5 レース分を含む)</summary>
        public const int UMABAN_TICKET_COLUMN_COUNT = 5;

        /// <summary>WIN5 のレース数</summary>
        public const int WIN5_RACE_COUNT = 5;

        /// <summary>
        /// <para>1 回の送信あたりの合計購入金額の上限(円)。</para>
        /// <para>I-PAT 側でも同じ上限が課されており、1 点でもこの上限が効くため
        /// 1 点あたりの金額の上限もこの値になる。</para>
        /// </summary>
        public const uint MAX_TOTAL_AMOUNT_PER_SEND = 1000000;

        /// <summary><see cref="Deposit"/> / <see cref="Withdraw"/> の既定リトライ回数</summary>
        public const ushort DEFAULT_RETRY_COUNT = 10;

        /// <summary><see cref="SetAutoDepositFlag"/> の既定入金額(円)</summary>
        public const uint DEPOSIT_DEFAULT_VALUE = 1000;

        /// <summary>残高反映を待つ既定のタイムアウト(ms)</summary>
        public const ushort DEFAULT_CONFIRM_TIMEOUT = 10000;

        /// <summary>
        /// <para>分割送信の間隔(ms)の既定値。<b>タイムアウトではない。</b></para>
        /// <para>本ラッパーの <see cref="Bet"/> / <see cref="BetWin5"/> は、これより余裕を持たせた
        /// <see cref="DEFAULT_BET_INTERVAL_MANAGED"/> を既定値として渡す。</para>
        /// </summary>
        public const ushort DEFAULT_BET_INTERVAL = 500;

        /// <summary>
        /// 本ラッパーの <see cref="Bet"/> / <see cref="BetWin5"/> が使う分割送信の間隔(ms)。
        /// DLL の既定値より保守的に取ってある。
        /// </summary>
        public const ushort DEFAULT_BET_INTERVAL_MANAGED = 1000;

        /// <summary><see cref="BetWin5Auto"/> で生成させられる点数の上限</summary>
        public const ushort MAX_WIN5_AUTO_BET_COUNT = 50;
        #endregion

        #region 構造体
        /// <summary>
        /// <para>馬券 1 点分の詳細情報。<b>購入時の指定そのものではなく、投票内容から復元した値</b>。</para>
        /// <para><see cref="horseNo"/> の各列が何を指すかは <see cref="method"/>(方式)と
        /// <see cref="type"/>(式別)の組み合わせで変わる。列を機械的に "-" で連結すると
        /// 誤った買い目になる(README「購入明細の読み方」を参照)。</para>
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ST_TICKET_DATA_DETAIL
        {
            /// <summary>
            /// 確定フラグ(<see cref="DECISIONFLAG"/>)。
            /// <see cref="DECISIONFLAG.PARSE_FAILED"/>(0)はその明細を解析できなかったことを表す。
            /// </summary>
            public byte decisionFlag;

            /// <summary>
            /// <para>券種(<see cref="BET_FLAG"/>)。通常 / WIN5 / 海外のいずれか。</para>
            /// <para>中央の購入履歴には海外の馬券も並ぶため、区別にはこの値を使う。</para>
            /// </summary>
            public byte betFlag;

            /// <summary>開催場(<see cref="Kaisai"/>)。WIN5 の明細では 0xFF</summary>
            public ushort kaisai;

            /// <summary>レース番号。WIN5 の明細では 0xFF</summary>
            public byte raceNo;

            /// <summary>週</summary>
            public byte week;

            /// <summary>方式(<see cref="Houshiki"/> と同じ値)。WIN5 の明細では 0xFF</summary>
            public byte method;

            /// <summary>式別(<see cref="Shikibetsu"/> と同じ値)。WIN5 の明細では 0xFF</summary>
            public byte type;

            /// <summary>
            /// <para>買い目(列ごとの馬番ビットフラグ)。bit 0 が馬番 1。
            /// 判定は <c>(horseNo[列] &amp; (1u &lt;&lt; (馬番 - 1))) != 0</c>。</para>
            /// <para>枠連だけは馬番ではなく枠番(bit 0 が枠 1)。使わない列は 0。
            /// <c>horseNo[3]</c> / <c>horseNo[4]</c> は WIN5 専用。</para>
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UMABAN_TICKET_COLUMN_COUNT)]
            public uint[] horseNo;

            /// <summary>
            /// <para>マルチかどうか(0:通常 1:マルチ)。</para>
            /// <para><b>マルチの判定は必ずこの値で行うこと。</b><see cref="method"/> では判定できない
            /// (マルチは基底のながし方式のまま記録されることがある)。</para>
            /// </summary>
            public byte multi;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct ST_TICKET_DATA_INTERNAL
        {
            public byte dayFlag;
            public byte receiptNo;
            public byte hour;
            public byte minute;
            public uint kingaku;
            public uint payout;
            public uint detailCount;
            public IntPtr detailData;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct ST_PURCHASE_DATA_INTERNAL
        {
            public ushort remainBetCount;
            public uint balance;
            public uint dayPurchase;
            public uint dayHaraimodosi;
            public uint totalPurchase;
            public uint totalHaraimodosi;
            public uint ticketCount;
            public IntPtr ticketData;
        };

        /// <summary>
        /// 1 受付分の馬券基本情報(利用者向け)
        /// </summary>
        public struct ST_TICKET_DATA
        {
            /// <summary>購入日フラグ(<see cref="DAY_TYPE"/>: 1:当日 / 2:前日)</summary>
            public byte dayFlag;

            /// <summary>受付番号</summary>
            public byte receiptNo;

            /// <summary>購入時刻(時)。応答から時刻を読み取れなかった受付では 0</summary>
            public byte hour;

            /// <summary>購入時刻(分)。応答から時刻を読み取れなかった受付では 0</summary>
            public byte minute;

            /// <summary>購入金額(円)</summary>
            public uint kingaku;

            /// <summary>払戻金額(円)</summary>
            public uint payout;

            /// <summary>明細の件数</summary>
            public uint detailCount;

            /// <summary>明細の配列</summary>
            public ST_TICKET_DATA_DETAIL[] detailData;
        };

        /// <summary>
        /// 馬券購入履歴全体(利用者向け)
        /// </summary>
        public struct ST_PURCHASE_DATA
        {
            /// <summary>残購入可能件数</summary>
            public ushort remainBetCount;

            /// <summary>現在の残高(円)</summary>
            public uint balance;

            /// <summary>当日購入金額(円)</summary>
            public uint dayPurchase;

            /// <summary>当日払戻金額(円)</summary>
            public uint dayHaraimodosi;

            /// <summary>累計購入金額(円)</summary>
            public uint totalPurchase;

            /// <summary>累計払戻金額(円)</summary>
            public uint totalHaraimodosi;

            /// <summary>馬券(受付)の件数</summary>
            public uint ticketCount;

            /// <summary>馬券(受付)の配列。中央 → 地方の順に連結される</summary>
            public ST_TICKET_DATA[] ticketData;
        };

        /// <summary>
        /// 馬券購入情報。<see cref="GetBetInstance"/> で構築して <see cref="Bet"/> へ渡す。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ST_BET_DATA
        {
            /// <summary>開催場(<see cref="Kaisai"/>)</summary>
            public ushort kaisai;

            /// <summary>レース番号(1〜14)</summary>
            public byte raceNo;

            /// <summary>曜日(<see cref="WEEK_DAY"/>)</summary>
            public byte youbi;

            /// <summary>方式(<see cref="Houshiki"/>)</summary>
            public byte houshiki;

            /// <summary>式別(<see cref="Shikibetsu"/>)</summary>
            public byte shikibetsu;

            /// <summary>1 点あたりの金額(円)</summary>
            public uint kingaku;

            /// <summary>買い目(列ごとの馬番ビットフラグ。bit 0 が馬番 1)</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UMABAN_COLUMN_COUNT)]
            public uint[] horseNo;

            /// <summary>
            /// 合計購入金額(円)。<see cref="GetBetInstance"/> が自動計算する。
            /// 応援馬券は指定金額の 2 倍になる。
            /// </summary>
            public uint totalAmount;

            /// <summary>マルチかどうか(0:通常 1:マルチ)。GetBetInstance がマルチ指定時に設定。</summary>
            public byte multi;
        };

        /// <summary>
        /// 馬券購入情報(WIN5)。<see cref="GetBetInstanceWin5"/> で構築して <see cref="BetWin5"/> へ渡す。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ST_BET_DATA_WIN5
        {
            /// <summary>1 点あたりの金額(円)</summary>
            public uint kingaku;

            /// <summary>曜日(<see cref="WEEK_DAY"/>)</summary>
            public byte youbi;

            /// <summary>第 1〜第 5 レースの買い目(馬番ビットフラグ。bit 0 が馬番 1)</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = WIN5_RACE_COUNT)]
            public uint[] horseNo;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct ST_ODDS_DETAIL
        {
            public byte type;       // 式別(Shikibetsu)
            public byte horse1;     // 馬番/枠番1
            public byte horse2;     // 馬番/枠番2(単勝・複勝は0)
            public byte horse3;     // 馬番3(三連複・三連単のみ、それ以外0)
            public byte status;     // 0:通常 1:発売中止 2:オッズ未取得
            public uint odds;       // オッズ×10(複勝・ワイドは下限)
            public uint oddsHigh;   // 複勝・ワイドの上限×10(それ以外0)
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct ST_ODDS_DATA_INTERNAL
        {
            public ushort place;
            public byte raceNo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] oddsTime;
            public uint detailCount;
            public IntPtr detailData;
        };

        public struct ST_ODDS_DATA
        {
            public ushort place;
            public byte raceNo;
            public string oddsTime;
            public uint detailCount;
            public ST_ODDS_DETAIL[] oddsDetail;
        };

        /// <summary>
        /// 出走馬明細(マーシャリング用 / C++のST_ENTRY_DETAILと同一レイアウト)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ST_ENTRY_DETAIL_INTERNAL
        {
            public byte ucWakuban;
            public byte ucUmaban;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szHorseName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] szSex;
            public byte ucAge;
            public byte ucWeightStatus;
            public ushort usWeight;
            public byte ucWeightDiffCode;
            public ushort usWeightDiff;
            public byte ucApprentice;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public byte[] szJockeyName;
            public ushort usBurden;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public byte[] szTrainerName;
            public ushort usWinPopular;
            public byte ucWinOddsStatus;
            public uint unWinOdds;
            public byte ucPlaceOddsStatus;
            public uint unPlaceOddsLow;
            public uint unPlaceOddsHigh;
        }

        /// <summary>
        /// 出走馬明細(利用者向け / 文字列はUTF-8をデコード済み)
        /// </summary>
        public struct ST_ENTRY_DETAIL
        {
            public byte wakuban;            // 枠番
            public byte umaban;             // 馬番
            public string horseName;        // 馬名
            public string sex;              // 性別(牡/牝/セン等)
            public byte age;                // 年齢
            public byte weightStatus;       // 馬体重状態(0:通常 1:未発表 2:出走取消 3:計量不能)
            public ushort weight;           // 馬体重(kg)。状態が0以外の場合は0
            public byte weightDiffCode;     // 増減符号(0:なし 1:増 2:減 3:増減なし 7:初出走 8:前計不)
            public ushort weightDiff;       // 増減量(kg)
            public byte apprentice;         // 見習騎手コード(0:なし 1〜5:減量 9:女性騎手2kg減)
            public string jockeyName;       // 騎手名
            public ushort burden;           // 斤量×10(例:57.0kg→570)
            public string trainerName;      // 調教師名
            public ushort winPopular;       // 単勝人気(0:データなし)
            public byte winOddsStatus;      // 単勝オッズ状態(0:通常 1:発売中止 2:未取得)
            public uint winOdds;            // 単勝オッズ×10
            public byte placeOddsStatus;    // 複勝オッズ状態(0:通常 1:発売中止 2:未取得)
            public uint placeOddsLow;       // 複勝オッズ下限×10
            public uint placeOddsHigh;      // 複勝オッズ上限×10
        };

        /// <summary>
        /// 出馬表情報(マーシャリング用 / 明細配列はIntPtrで受ける)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ST_RACECARD_DATA_INTERNAL
        {
            public ushort usPlace;
            public byte ucRaceNo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] szOddsTime;
            public uint unEntryCount;
            public IntPtr pobjEntry;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] szRaceName;
            // ネイティブ側の ST_RACECARD_DATA へ後から追加されたフィールド。
            // この構造体はネイティブ側が直接書き込む領域のため、
            // 順序・型が DLL 側と一致していないとメモリ破壊になる。
            // 末尾へ勝手にフィールドを足さないこと(DLL が書かない領域を読むだけになる)。
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] szDeadline;
            public byte ucRaceStatus;
        }

        /// <summary>
        /// 出馬表情報(利用者向け)
        /// </summary>
        public struct ST_RACECARD_DATA
        {
            public ushort place;            // 開催場(入力値)
            public byte raceNo;             // レース番号(入力値)
            public string oddsTime;         // オッズ更新時刻 "HH:MM"
            public uint entryCount;         // 出走馬数
            public ST_ENTRY_DETAIL[] entries; // 出走馬明細
            public string raceName;         // レース名(取得不可時は空文字。海外開催でも取得できる)
            public string deadline;         // 発売締切時刻 "HH:MM"(取得不可時は空文字)
            public RACE_STATUS raceStatus;  // 発売状態(締切時刻だけでは購入可否が分からないため併用する)
        };

        /// <summary>
        /// お知らせ一覧の1件分(マーシャリング用)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ST_NOTICE_ITEM_INTERNAL
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] szTitle;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szDate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
            public byte[] szUrl;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] szIcon;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szColor;
        }

        /// <summary>
        /// お知らせ情報(マーシャリング用 / 一覧配列はIntPtrで受ける)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ST_NOTICE_DATA_INTERNAL
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]
            public byte[] szMessage;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szNoticeNo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] szNoticeType;
            public uint unItemCount;
            public IntPtr pobjItem;
        }

        /// <summary>
        /// お知らせ一覧の1件分(利用者向け / 文字列はUTF-8をデコード済み)
        /// </summary>
        public struct ST_NOTICE_ITEM
        {
            public string title;    // タイトル
            public string date;     // 日付テキスト
            public string url;      // リンクURL
            public string icon;     // アイコンファイル名
            public string color;    // 日付表示色
        };

        /// <summary>
        /// 開催中のレース1つ分(マーシャリング用)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ST_KAISAI_RACE_INTERNAL
        {
            public byte ucRaceNo;
            public byte ucRaceStatus;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] szDeadline;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] szRaceName;
        }

        /// <summary>
        /// 開催中の開催場1つ分(マーシャリング用 / レース配列はIntPtrで受ける)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ST_KAISAI_ITEM_INTERNAL
        {
            public ushort usPlace;
            public uint unRaceCount;
            public IntPtr pobjRace;
        }

        /// <summary>
        /// 開催場一覧(マーシャリング用 / 開催場配列はIntPtrで受ける)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ST_KAISAI_DATA_INTERNAL
        {
            public uint unKaisaiCount;
            public IntPtr pobjKaisai;
        }

        /// <summary>
        /// 開催中のレース1つ分(利用者向け / 文字列はUTF-8をデコード済み)
        /// </summary>
        public struct ST_KAISAI_RACE
        {
            /// <summary>レース番号(1 始まり)</summary>
            public byte raceNo;

            /// <summary>
            /// 発売状態。締切時刻だけでは購入可否が判断できないため併せて参照すること。
            /// </summary>
            public RACE_STATUS raceStatus;

            /// <summary>発売締切時刻 "HH:MM"。取得できない場合は空文字</summary>
            public string deadline;

            /// <summary>レース名。取得できない場合は空文字(海外開催でも取得できる)</summary>
            public string raceName;
        };

        /// <summary>
        /// 開催中の開催場1つ分(利用者向け)
        /// </summary>
        public struct ST_KAISAI_ITEM
        {
            /// <summary>開催場</summary>
            public Kaisai place;

            /// <summary>レース数</summary>
            public uint raceCount;

            /// <summary>
            /// <para>レース一覧。</para>
            /// <para>レース番号順に並ぶが、欠番があり得るため
            /// <b>レース番号は <see cref="ST_KAISAI_RACE.raceNo"/> で判断すること</b>
            /// (添字 + 1 と一致するとは限らない)。</para>
            /// </summary>
            public ST_KAISAI_RACE[] races;
        };

        /// <summary>
        /// 本日の開催場一覧(利用者向け)
        /// </summary>
        public struct ST_KAISAI_DATA
        {
            /// <summary>開催場数</summary>
            public uint kaisaiCount;

            /// <summary>開催場一覧</summary>
            public ST_KAISAI_ITEM[] kaisai;
        };

        /// <summary>
        /// お知らせ情報(利用者向け)
        /// </summary>
        public struct ST_NOTICE_DATA
        {
            public string message;          // 強制表示お知らせ本文。無い場合は空文字
            public string noticeNo;         // お知らせ番号
            public string noticeType;       // お知らせ種別
            public uint itemCount;          // お知らせ一覧の件数
            public ST_NOTICE_ITEM[] items;  // お知らせ一覧
        };
        #endregion

        #region 列挙体
        /// <summary>
        /// <para>開催場。値は固定されており、開催場が追加される場合は必ず末尾へ追加される。</para>
        /// <para>SAPPORO〜KOKURA が中央、SONODA〜SAGA が地方、LONGCHAMP 以降が海外。</para>
        /// </summary>
        public enum Kaisai
        {
            SAPPORO,
            HAKODATE,
            FUKUSHIMA,
            NIIGATA,
            TOKYO,
            NAKAYAMA,
            CHUKYO,
            KYOTO,
            HANSHIN,
            KOKURA,
            SONODA,
            HIMEJI,
            NAGOYA,
            MONBETSU,
            MORIOKA,
            MIZUSAWA,
            URAWA,
            FUNABASHI,
            OI,
            KAWASAKI,
            KASAMATSU,
            KANAZAWA,
            KOCHI,
            SAGA,
            LONGCHAMP,
            SHATIN,
            SANTAANITA,

            /// <summary>ドーヴィル</summary>
            DEAUVILLE,

            /// <summary>ドーヴィル(綴りを誤った旧名。<see cref="DEAUVILLE"/> と同じ値)</summary>
            [Obsolete("綴りを修正した DEAUVILLE を使用してください。値は同じです。")]
            DEAUVILE = DEAUVILLE,

            CHURCHILLDOWNS,
            ABDULAZIZ,
            ASCOT
        }

        /// <summary>
        /// <para>方式。ながし系(WHEEL_*)は買い目の列(ハイフン区切り)の意味が式別により異なる。</para>
        /// <para>マルチ(WHEEL_MULTI_*)は馬単・三連単のみ有効で、<see cref="GetBetInstance"/> が
        /// 内部で基底のながし方式＋マルチフラグへ変換する。</para>
        /// </summary>
        public enum Houshiki
        {
            /// <summary>通常</summary>
            NORMAL = 0,
            /// <summary>フォーメーション</summary>
            FORMATION = 1,
            /// <summary>ボックス</summary>
            BOX = 2,
            /// <summary>軸1頭ながし(1着流し)/馬連・ワイド・枠連ながし/三連複軸1頭/三連単1着ながし。買い目「軸-相手」</summary>
            WHEEL_1ST = 3,
            /// <summary>2着ながし(馬単・三連単)。買い目「軸-相手」</summary>
            WHEEL_2ND = 4,
            /// <summary>3着ながし(三連単)。買い目「軸-相手」</summary>
            WHEEL_3RD = 5,
            /// <summary>軸2頭ながし(三連複)/1・2着ながし(三連単)。三連複「軸,軸-相手」/三連単「1着軸-2着軸-相手」</summary>
            WHEEL_1ST_2ND = 6,
            /// <summary>1・3着ながし(三連単)。買い目「1着軸-相手-3着軸」</summary>
            WHEEL_1ST_3RD = 7,
            /// <summary>2・3着ながし(三連単)。買い目「相手-2着軸-3着軸」</summary>
            WHEEL_2ND_3RD = 8,
            /// <summary>軸1頭ながしマルチ(馬単・三連単)。買い目「軸-相手」</summary>
            WHEEL_MULTI_AXIS1 = 9,
            /// <summary>軸2頭ながしマルチ(三連単)。買い目「軸-軸-相手」</summary>
            WHEEL_MULTI_AXIS2 = 10
        }

        /// <summary>
        /// 式別
        /// </summary>
        public enum Shikibetsu
        {
            /// <summary>単勝</summary>
            WIN = 1,
            /// <summary>複勝</summary>
            PLACE,
            /// <summary>
            /// <para>枠連。買い目は馬番ではなく<b>枠番(1〜8)</b>で指定する。</para>
            /// <para>ゾロ目は同じ枠を 2 つ("3-3")。通常方式で枠 1 つだけの "3" も同じ意味。</para>
            /// <para>海外開催では購入できない(枠の概念が無いため)。</para>
            /// </summary>
            BRACKETQUINELLA,
            /// <summary>馬連</summary>
            QUINELLA,
            /// <summary>ワイド</summary>
            QUINELLAPLACE,
            /// <summary>馬単</summary>
            EXACTA,
            /// <summary>三連複</summary>
            TRIO,
            /// <summary>三連単</summary>
            TRIFECTA,
            /// <summary>
            /// <para>応援馬券(同一馬の単勝＋複勝のセット)。方式は <see cref="Houshiki.NORMAL"/>・馬番 1 頭のみ。</para>
            /// <para><b>合計購入金額は指定金額の 2 倍</b>になる(100 円指定 = 単勝 100 円 + 複勝 100 円)。
            /// 点数も 2 点として数える。</para>
            /// <para>送信時に単勝と複勝の 2 点へ展開されるため、
            /// <b>購入履歴には単勝と複勝が別々の馬券として現れる</b>。</para>
            /// <para><see cref="GetOdds"/> にこの式別は指定できない(単勝・複勝を個別に取得すること)。</para>
            /// </summary>
            WINPLACE
        }

        /// <summary>
        /// 購入日種類(<see cref="ST_TICKET_DATA.dayFlag"/>)
        /// </summary>
        public enum DAY_TYPE
        {
            /// <summary>当日</summary>
            TODAY = 1,
            /// <summary>前日</summary>
            BEFORE
        }

        /// <summary>
        /// レースの発売状態(<see cref="ST_RACECARD_DATA.raceStatus"/>)。
        /// UNKNOWN が 0 ではないのは、0 が「発売中」でありゼロ初期化と区別する必要があるため。
        /// </summary>
        public enum RACE_STATUS : byte
        {
            ON_SALE = 0,        // 発売中
            CLOSED = 1,         // 発売終了
            CANCELED = 2,       // 発売中止
            BEFORE_SALE = 3,    // 発売前
            UNKNOWN = 0xFF      // 取得できなかった
        }

        /// <summary>
        /// 戻り値のビットフラグ。複数のフラグが同時に立つことがある。
        /// </summary>
        [Flags]
        public enum RETURN_VALUE : uint
        {
            /// <summary>処理に成功</summary>
            SUCCESS = 1,
            /// <summary>処理に失敗(パラメータ不正・残高不足・未ログイン等)</summary>
            UNSUCCESS = 2,
            /// <summary>中央競馬での処理に失敗</summary>
            FAILED_CHUOU = 4,
            /// <summary>地方競馬での処理に失敗</summary>
            FAILED_CHIHOU = 8,
            /// <summary>中央競馬での通信に失敗</summary>
            FAILED_COMMUNICATE_CHUOU = 16,
            /// <summary>地方競馬での通信に失敗</summary>
            FAILED_COMMUNICATE_CHIHOU = 32,

            /// <summary>
            /// <para>サービス時間外(ログインフォームが提供されていない)。</para>
            /// <para><see cref="Login"/> でのみ立ち、<see cref="FAILED_CHUOU"/> /
            /// <see cref="FAILED_CHIHOU"/> と<b>併せて</b>立つ。</para>
            /// <para>最も多い原因は投票受付時間外(特に地方競馬は営業時間外に必ずこの状態になる)。
            /// メンテナンス中も同じ状態になり得るため両者は区別できない。</para>
            /// <para><b>即座のリトライは必ず失敗する。</b>時間をおいて再試行すること。</para>
            /// </summary>
            FAILED_OUT_OF_SERVICE = 64,
        }

        /// <summary>
        /// 曜日(<see cref="ST_BET_DATA.youbi"/>)
        /// </summary>
        public enum WEEK_DAY
        {
            /// <summary>日曜日</summary>
            SUNDAY = 1,
            /// <summary>月曜日</summary>
            MONDAY,
            /// <summary>火曜日</summary>
            TUESDAY,
            /// <summary>水曜日</summary>
            WEDNESDAY,
            /// <summary>木曜日</summary>
            THURSDAY,
            /// <summary>金曜日</summary>
            FRIDAY,
            /// <summary>土曜日</summary>
            SATURDAY
        }

        /// <summary>
        /// 確定フラグ(<see cref="ST_TICKET_DATA_DETAIL.decisionFlag"/>)
        /// </summary>
        public enum DECISIONFLAG
        {
            /// <summary>
            /// <para>その明細を解析できなかったことを表す。</para>
            /// <para>他の明細・他の受付は正常に返される。この明細は
            /// <see cref="ST_TICKET_DATA_DETAIL.betFlag"/> にその受付の券種が入り、
            /// それ以外のフィールドはすべて 0 になる。</para>
            /// <para>金額は明細ではなく <see cref="ST_TICKET_DATA.kingaku"/> /
            /// <see cref="ST_TICKET_DATA.payout"/> に入っているため、集計には影響しない。</para>
            /// </summary>
            PARSE_FAILED = 0,

            DEFAULT = 1,
            NORMAL,
            DEADLINE,
            CANCEL,
            FLATMATESCANCEL,
            HIT,
            MISS,
            BACK,
            PARTCANCEL,
            INVALID,
            SALECANCEL
        }

        /// <summary>
        /// 券種(<see cref="ST_TICKET_DATA_DETAIL.betFlag"/>)
        /// </summary>
        public enum BET_FLAG
        {
            /// <summary>通常</summary>
            NORMAL,
            /// <summary>WIN5</summary>
            WIN5,
            /// <summary>海外(中央の購入履歴に混在する)</summary>
            INTERNATIONAL
        }
        #endregion

        #region ログ
        /// <summary>
        /// ログレベル。SetLogCallback の minLevel に指定する。
        /// </summary>
        public enum LogLevel
        {
            /// <summary>詳細トレース。入出金失敗時の応答本文の抜粋はこのレベルでのみ通知される。</summary>
            Trace = 0,
            /// <summary>情報 (既定)</summary>
            Info = 1,
            /// <summary>警告</summary>
            Warn = 2,
            /// <summary>エラー。失敗した段階・画面ID・タイトルはこのレベルで通知される。</summary>
            Error = 3,
        }

        /// <summary>
        /// DLL 内部のログを受け取るハンドラ。
        /// </summary>
        /// <param name="level">ログレベル</param>
        /// <param name="message">ログ本文 (UTF-8 からデコード済み)</param>
        public delegate void LogHandler(LogLevel level, string message);

        // ネイティブ側へ渡すデリゲートは GC されるとコールバック時にクラッシュするため、
        // 静的フィールドで参照を保持する。
        private static NativeMethods.LogCallback _nativeLogCallback;
        private static LogHandler _logHandler;

        /// <summary>
        /// <para>DLL 内部のログを受け取るハンドラを登録する。null で解除。</para>
        /// <para>入出金は erc/erm のようなエラーコードを返さないため、失敗の原因を知るには
        /// このログが唯一の手掛かりになる。失敗した段階・画面ID・タイトルは
        /// <see cref="LogLevel.Error"/> で通知されるが、サーバ側の拒否理由が載る
        /// 応答本文の抜粋は <see cref="LogLevel.Trace"/> を指定したときのみ通知される。</para>
        /// <para>注意: ハンドラは DLL 内部ロックを保持したまま呼ばれるため、
        /// ハンドラ内から本クラスの API を呼び返さないこと (デッドロックする)。
        /// また Login 中は中央・地方の 2 スレッドから同時に呼ばれる。</para>
        /// <para>応答本文の抜粋には口座番号や残高が含まれ得るため、
        /// Trace は調査時のみ指定し、ログの取り扱いに注意すること。</para>
        /// </summary>
        /// <param name="handler">ログハンドラ (null で解除)</param>
        /// <param name="minLevel">通知する最小レベル</param>
        public static void SetLogCallback(LogHandler handler, LogLevel minLevel = LogLevel.Info)
        {
            _logHandler = handler;

            if (handler == null)
            {
                // 解除時は DLL 側が排他ロックを取るため、戻った時点で実行中の呼び出しは無い
                NativeMethods.SetLogCallback(null, (int)minLevel);
                _nativeLogCallback = null;
                return;
            }

            _nativeLogCallback = (level, message) =>
            {
                // message は UTF-8 の null 終端文字列。既定のマーシャリング (ANSI) では
                // 日本語が化けるため IntPtr で受けて明示的にデコードする。
                string text = message == IntPtr.Zero ? string.Empty : PtrToStringUtf8(message);
                try { _logHandler?.Invoke((LogLevel)level, text); }
                catch { /* ハンドラ側の例外をネイティブへ伝播させない */ }
            };
            NativeMethods.SetLogCallback(_nativeLogCallback, (int)minLevel);
        }

        private static string PtrToStringUtf8(IntPtr ptr)
        {
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0) len++;
            if (len == 0) return string.Empty;
            byte[] buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, len);
            return Encoding.UTF8.GetString(buffer);
        }
        #endregion

        #region 内部クラス
        private class NativeMethods
        {
            // ネイティブ側の呼び出し規約は __cdecl。x86 では C# の既定 (Winapi=StdCall) と
            // 異なるため明示が必須。
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void LogCallback(int nLevel, IntPtr pszMessage);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void SetLogCallback(LogCallback callback, int nMinLevel);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint Login(byte[] arybyINetId, byte[] arybyId, byte[] arybyPassword, byte[] arybyPars);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint Logout();

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint Deposit(uint unDepositValue, ushort usRetryCount);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint Withdraw(ushort usRetryCount);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetPurchaseData(ref ST_PURCHASE_DATA_INTERNAL objPurchaseData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void ReleasePurchaseData(ref ST_PURCHASE_DATA_INTERNAL objPurchaseData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetBetInstance(ushort usKaisai,
                                                      byte byRaceNo,
                                                      ushort usYear,
                                                      byte byMonth,
                                                      byte byDay,
                                                      byte byHoushiki,
                                                      byte byShikibetsu,
                                                      uint unKingaku,
                                                      byte[] arybyKaime,
                                                      ref ST_BET_DATA objBetData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetBetInstanceWin5(uint unKingaku,
                                                          ushort usYear,
                                                          byte byMonth,
                                                          byte byDay,
                                                          byte[] arybyKaime,
                                                          ref ST_BET_DATA_WIN5 objBetData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint Bet([In, Out] ST_BET_DATA[] lstBetData, ushort usBetCount, ushort usWaitMiliSeconds);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint BetWin5(ST_BET_DATA_WIN5 objBetData, ushort usWaitMiliSeconds);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint BetWin5Auto(byte ucMode, byte[] arybyAxisUmaban,
                ushort usBetCount, uint unKingaku, ushort usYear, byte ucMonth, byte ucDay);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint SetAutoDepositFlag([MarshalAs(UnmanagedType.I1)] bool bEnable, uint unDepositValue, ushort usConfrimTimeout);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetOdds(ushort usKaisai, byte byRaceNo, byte byShikibetsu, ref ST_ODDS_DATA_INTERNAL objOddsData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void ReleaseOddsData(ref ST_ODDS_DATA_INTERNAL objOddsData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetRaceCard(ushort usPlace, byte byRaceNo, ref ST_RACECARD_DATA_INTERNAL objRaceCardData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void ReleaseRaceCardData(ref ST_RACECARD_DATA_INTERNAL objRaceCardData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetNotice(ref ST_NOTICE_DATA_INTERNAL objNoticeData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void ReleaseNoticeData(ref ST_NOTICE_DATA_INTERNAL objNoticeData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetKaisaiList(ref ST_KAISAI_DATA_INTERNAL objKaisaiData);

            [DllImport("IpatHelper.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void ReleaseKaisaiData(ref ST_KAISAI_DATA_INTERNAL objKaisaiData);
        }
        #endregion

        #region 公開関数
        /// <summary>
        /// <para>I-PAT へログインする。中央競馬と地方競馬へ並列でログインを試みる。</para>
        /// <para>他のすべての API を呼び出す前に必ず実行すること。</para>
        /// <para>どちらか一方でも成功すれば <see cref="RETURN_VALUE.SUCCESS"/> が立つ。
        /// 失敗した系統は <see cref="RETURN_VALUE.FAILED_CHUOU"/> /
        /// <see cref="RETURN_VALUE.FAILED_CHIHOU"/> で判別する。</para>
        /// <para>受付時間外・メンテナンス中は、それらと併せて
        /// <see cref="RETURN_VALUE.FAILED_OUT_OF_SERVICE"/> が立つ。この場合の即時リトライは
        /// 必ず失敗するため、時間をおいて再試行すること。</para>
        /// </summary>
        /// <param name="iNetId">I-NET ID</param>
        /// <param name="id">ログイン ID(加入者番号)</param>
        /// <param name="password">パスワード</param>
        /// <param name="pars">P-ARS 番号</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint Login(string iNetId, string id, string password, string pars)
        {
            return NativeMethods.Login(ToNullTerminatedUtf8(iNetId),
                                       ToNullTerminatedUtf8(id),
                                       ToNullTerminatedUtf8(password),
                                       ToNullTerminatedUtf8(pars));
        }

        /// <summary>
        /// <para>I-PAT からログアウトし、内部セッション情報・自動入金設定を初期化する。</para>
        /// <para>サーバへの通知に失敗しても後始末は必ず行われ、本 API 自体は成功を返す。</para>
        /// </summary>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint Logout()
        {
            return NativeMethods.Logout();
        }

        /// <summary>
        /// <para>登録口座から I-PAT 口座へ入金する。</para>
        /// <para>入金指示の完了後、入金額が残高へ加算されたことを確認できるまで待機し、
        /// 反映を確認できた場合のみ成功を返す(待機時間の上限は
        /// <see cref="SetAutoDepositFlag"/> の confirmTimeout)。</para>
        /// <para><b>即PAT(ネットバンク)会員専用。</b>A-PAT 会員は
        /// <see cref="RETURN_VALUE.UNSUCCESS"/> になる。</para>
        /// <para><b>登録口座が PayPay(コード決済アプリ)の場合は利用できない。</b>
        /// 通信を行わず <see cref="RETURN_VALUE.UNSUCCESS"/> を返す。
        /// PayPay<b>銀行</b>は従来どおり利用できる(別物)。</para>
        /// </summary>
        /// <param name="depositValue">入金額(円・100 円単位)</param>
        /// <param name="retryCount">
        /// <para>リトライ回数。適用されるのは<b>入金実行前の準備段階のみ</b>。</para>
        /// <para>入金実行そのものは、応答を受信できなくてもサーバ側で成立している可能性があるため
        /// 再送しない(二重入金の防止)。成否は残高への反映で判定する。</para>
        /// </param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint Deposit(uint depositValue, ushort retryCount = DEFAULT_RETRY_COUNT)
        {
            return NativeMethods.Deposit(depositValue, retryCount);
        }

        /// <summary>
        /// <para>I-PAT 口座から登録口座へ<b>全額</b>出金する。</para>
        /// <para>出金指示の完了後、残高が 0 になったことを確認できるまで待機し、
        /// 反映を確認できた場合のみ成功を返す。</para>
        /// <para><see cref="Deposit"/> と同じく<b>即PAT(ネットバンク)会員専用</b>で、
        /// 登録口座が PayPay(コード決済アプリ)の場合は利用できない。</para>
        /// </summary>
        /// <param name="retryCount">
        /// リトライ回数。<see cref="Deposit"/> と同じく準備段階にのみ適用され、
        /// 出金の実行そのものは再送しない(二重出金の防止)。
        /// </param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint Withdraw(ushort retryCount = DEFAULT_RETRY_COUNT)
        {
            return NativeMethods.Withdraw(retryCount);
        }

        /// <summary>
        /// <para>当日・前日の馬券購入履歴を取得する。ネイティブ側のメモリ解放はラッパー内部で行う。</para>
        /// <para>購入履歴は会場ごとに別のサイトが保持しているため、<b>ログイン済みの会場すべてから
        /// 取得して連結</b>する(中央 → 地方の順)。海外の馬券は中央の履歴に含まれる。</para>
        /// <para>残高・購入可能件数・当日/累計の金額は<b>合算しない</b>
        /// (中央・地方は同じ即PAT 口座を共有するため)。</para>
        /// <para>片方の会場だけ取得に失敗した場合は、取得できた分を返したうえで
        /// <see cref="RETURN_VALUE.FAILED_CHUOU"/> / <see cref="RETURN_VALUE.FAILED_CHIHOU"/> を立てる
        /// (<see cref="RETURN_VALUE.SUCCESS"/> と同時に立つ)。履歴の欠けを検出したい場合は
        /// これらのフラグも確認すること。</para>
        /// </summary>
        /// <param name="purchaseData">取得した購入履歴</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint GetPurchaseData(out ST_PURCHASE_DATA purchaseData)
        {
            ST_PURCHASE_DATA_INTERNAL tempTicketData = new()
            {
                remainBetCount = 0,
                balance = 0,
                dayPurchase = 0,
                dayHaraimodosi = 0,
                totalPurchase = 0,
                totalHaraimodosi = 0,
                ticketCount = 0,
                ticketData = IntPtr.Zero
            };

            uint returnValue = NativeMethods.GetPurchaseData(ref tempTicketData);
            if ((returnValue & 1) != 1)
            {
                NativeMethods.ReleasePurchaseData(ref tempTicketData);
                purchaseData = new ST_PURCHASE_DATA();
                return returnValue;
            }

            purchaseData = new ST_PURCHASE_DATA()
            {
                remainBetCount = tempTicketData.remainBetCount,
                balance = tempTicketData.balance,
                dayPurchase = tempTicketData.dayPurchase,
                dayHaraimodosi = tempTicketData.dayHaraimodosi,
                totalPurchase = tempTicketData.totalPurchase,
                totalHaraimodosi = tempTicketData.totalHaraimodosi,
                ticketCount = tempTicketData.ticketCount,
                ticketData = new ST_TICKET_DATA[tempTicketData.ticketCount]
            };

            if (tempTicketData.ticketCount <= 0 || tempTicketData.ticketData == IntPtr.Zero)
            {
                purchaseData.ticketCount = 0;
                purchaseData.ticketData = Array.Empty<ST_TICKET_DATA>();
                NativeMethods.ReleasePurchaseData(ref tempTicketData);
                return returnValue;
            }

            // 構造体データ格納用バッファを確保する
            byte[] allTicketBytes = new byte[Marshal.SizeOf(typeof(ST_TICKET_DATA_INTERNAL)) * tempTicketData.ticketCount];

            // IntPtrからbyte配列に変換する
            Marshal.Copy(tempTicketData.ticketData, allTicketBytes, 0, allTicketBytes.Length);

            for (int i = 0; i < tempTicketData.ticketCount; i++)
            {
                // 1つの構造体サイズ分のポインタを確保する
                IntPtr tempPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ST_TICKET_DATA_INTERNAL)));

                // バイト配列から1つの構造体分のデータをコピーする
                Marshal.Copy(allTicketBytes, i * Marshal.SizeOf(typeof(ST_TICKET_DATA_INTERNAL)), tempPtr, Marshal.SizeOf(typeof(ST_TICKET_DATA_INTERNAL)));

                // ポインタを構造体に変換する
                ST_TICKET_DATA_INTERNAL tempTicket = (ST_TICKET_DATA_INTERNAL)Marshal.PtrToStructure(tempPtr, typeof(ST_TICKET_DATA_INTERNAL));

                // 使用したポインタを解放する
                Marshal.FreeHGlobal(tempPtr);

                purchaseData.ticketData[i] = new ST_TICKET_DATA()
                {
                    dayFlag = tempTicket.dayFlag,
                    receiptNo = tempTicket.receiptNo,
                    hour = tempTicket.hour,
                    minute = tempTicket.minute,
                    kingaku = tempTicket.kingaku,
                    payout = tempTicket.payout,
                    detailCount = tempTicket.detailCount,
                    detailData = new ST_TICKET_DATA_DETAIL[tempTicket.detailCount]
                };

                // 明細を持たない受付があっても、後続の受付は正常に返される。
                // ここで打ち切ると以降の馬券をすべて取りこぼすため次の受付へ進む。
                if (tempTicket.detailCount <= 0 || tempTicket.detailData == IntPtr.Zero)
                {
                    purchaseData.ticketData[i].detailCount = 0;
                    purchaseData.ticketData[i].detailData = Array.Empty<ST_TICKET_DATA_DETAIL>();
                    continue;
                }

                // 構造体データ格納用バッファを確保する
                byte[] allDetailBytes = new byte[Marshal.SizeOf(typeof(ST_TICKET_DATA_DETAIL)) * tempTicket.detailCount];

                // IntPtrからbyte配列に変換する
                Marshal.Copy(tempTicket.detailData, allDetailBytes, 0, allDetailBytes.Length);

                for (int j = 0; j < tempTicket.detailCount; j++)
                {
                    // 1つの構造体サイズ分のポインタを確保する
                    tempPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ST_TICKET_DATA_DETAIL)));

                    // バイト配列から1つの構造体分のデータをコピーする
                    Marshal.Copy(allDetailBytes, j * Marshal.SizeOf(typeof(ST_TICKET_DATA_DETAIL)), tempPtr, Marshal.SizeOf(typeof(ST_TICKET_DATA_DETAIL)));

                    // ポインタを構造体に変換する
                    purchaseData.ticketData[i].detailData[j] = (ST_TICKET_DATA_DETAIL)Marshal.PtrToStructure(tempPtr, typeof(ST_TICKET_DATA_DETAIL));

                    // 使用したポインタを解放する
                    Marshal.FreeHGlobal(tempPtr);
                }
            }

            NativeMethods.ReleasePurchaseData(ref tempTicketData);

            return returnValue;
        }

        /// <summary>
        /// <para>買い目文字列から馬券購入情報を構築する。<see cref="Bet"/> の前に必ず実行すること。</para>
        /// <para>通信もグローバル状態の参照も行わないため、他の API の通信中でも並行して呼び出せる。</para>
        /// </summary>
        /// <param name="place">開催場</param>
        /// <param name="raceNo">レース番号(1〜14)。範囲外は <see cref="RETURN_VALUE.UNSUCCESS"/></param>
        /// <param name="kaisaibi">開催日</param>
        /// <param name="houshiki">方式</param>
        /// <param name="shikibetsu">式別</param>
        /// <param name="kingaku">
        /// 1 点あたりの購入金額(100 円以上 <see cref="MAX_TOTAL_AMOUNT_PER_SEND"/> 円以下、100 円単位)
        /// </param>
        /// <param name="kaime">
        /// 買い目文字列。馬番は 1〜18(海外は 1〜24)。
        /// <b>範囲外の馬番が含まれる場合は黙って無視せず失敗する</b>(指定より少ない点数で購入されるのを防ぐため)。
        /// </param>
        /// <param name="betData">構築された購入情報。合計購入金額は totalAmount に入る</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint GetBetInstance(Kaisai place, byte raceNo, DateTime kaisaibi, Houshiki houshiki,
            Shikibetsu shikibetsu, uint kingaku, string kaime, out ST_BET_DATA betData)
        {
            betData = new ST_BET_DATA()
            {
                kaisai = 0,
                raceNo = 0,
                youbi = 0,
                houshiki = 0,
                shikibetsu = 0,
                kingaku = 0,
                horseNo = new uint[UMABAN_COLUMN_COUNT],
                totalAmount = 0,
                multi = 0
            };

            return NativeMethods.GetBetInstance((ushort)place, raceNo, (ushort)kaisaibi.Year, (byte)kaisaibi.Month, (byte)kaisaibi.Day, (byte)houshiki,
                                                       (byte)shikibetsu, kingaku, ToNullTerminatedUtf8(kaime), ref betData);
        }

        /// <summary>
        /// <para>WIN5 の買い目文字列から購入情報を構築する。<see cref="BetWin5"/> の前に必ず実行すること。</para>
        /// <para><see cref="GetBetInstance"/> と同じく内部ロックを取得しない。</para>
        /// </summary>
        /// <param name="kingaku">
        /// 1 点あたりの購入金額(100 円以上 <see cref="MAX_TOTAL_AMOUNT_PER_SEND"/> 円以下、100 円単位)
        /// </param>
        /// <param name="kaisaibi">開催日</param>
        /// <param name="kaime">買い目文字列(5 レース分。馬番は 1〜18)</param>
        /// <param name="objBetData">構築された購入情報</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint GetBetInstanceWin5(uint kingaku, DateTime kaisaibi, string kaime, out ST_BET_DATA_WIN5 objBetData)
        {
            objBetData = new ST_BET_DATA_WIN5()
            {
                youbi = 0,
                kingaku = 0,
                horseNo = new uint[WIN5_RACE_COUNT]
            };

            return NativeMethods.GetBetInstanceWin5(kingaku, (ushort)kaisaibi.Year, (byte)kaisaibi.Month, (byte)kaisaibi.Day, ToNullTerminatedUtf8(kaime), ref objBetData);
        }

        /// <summary>
        /// <para>馬券を購入する。異なる開催場の買い目も一括で渡せる(中央・地方・海外を自動振り分け)。</para>
        /// <para>1 回の送信上限(中央 255 件 / 地方 50 件)を超える場合は自動的に分割送信する。</para>
        /// <para>購入前に残高と購入可能件数を確認し、自動入金が有効なら残高不足時に入金する。</para>
        /// </summary>
        /// <param name="betDataList"><see cref="GetBetInstance"/> で構築した購入情報のリスト</param>
        /// <param name="waitMiliSeconds">
        /// <para><b>分割送信の間隔(ms)。タイムアウトではない。</b>間隔が短いと購入に失敗することがある。</para>
        /// <para>DLL 側の既定値は <see cref="DEFAULT_BET_INTERVAL"/>(500ms)だが、本ラッパーは
        /// より余裕を持たせた <see cref="DEFAULT_BET_INTERVAL_MANAGED"/>(1000ms)を既定で渡す。</para>
        /// </param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint Bet(List<ST_BET_DATA> betDataList, ushort waitMiliSeconds = DEFAULT_BET_INTERVAL_MANAGED)
        {
            return NativeMethods.Bet(betDataList.ToArray(), (ushort)betDataList.Count, waitMiliSeconds);
        }

        /// <summary>
        /// <para>WIN5 馬券を購入する(<b>中央競馬のみ</b>)。</para>
        /// <para>1 回の購入上限(50 組み合わせ)を超える場合は自動的に分割送信する。</para>
        /// </summary>
        /// <param name="betData"><see cref="GetBetInstanceWin5"/> で構築した購入情報</param>
        /// <param name="waitMiliSeconds">
        /// 分割送信の間隔(ms)。<see cref="Bet"/> と同じくタイムアウトではない。
        /// </param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint BetWin5(ST_BET_DATA_WIN5 betData, ushort waitMiliSeconds = DEFAULT_BET_INTERVAL_MANAGED)
        {
            return NativeMethods.BetWin5(betData, waitMiliSeconds);
        }

        /// <summary>
        /// WIN5 の購入方式 (BetWin5Auto)
        /// </summary>
        public enum Win5AutoMode
        {
            /// <summary>セレクト: 軸馬を指定し、残りはサーバが選ぶ</summary>
            Select = 2,
            /// <summary>ランダム: すべてサーバが選ぶ</summary>
            Random = 3,
        }

        /// <summary>
        /// <para>WIN5 を「セレクト」または「ランダム」で購入する (中央競馬のみ)。</para>
        /// <para>買い目を指定する <see cref="BetWin5"/> と違い、買い目はサーバが生成する。
        /// 生成された買い目はそのまま購入されるため、内容を事前に確認する手段は無い。</para>
        /// <para><b>実際に購入が行われる。</b>呼び出す前に必ず利用者の確認を取ること。</para>
        /// </summary>
        /// <param name="mode">購入方式</param>
        /// <param name="axisUmaban">
        /// <para>セレクト時の軸馬番。5 レース分をカンマ区切りで指定する (例 "3,0,7,0,0")。
        /// 0 のレースはサーバが選ぶ。ランダム時は無視される (null 可)。</para>
        /// <para><b>0(おまかせ)にできるのは 1〜4 レース。</b>次の 2 つは送信せずに
        /// <see cref="RETURN_VALUE.UNSUCCESS"/> を返す。</para>
        /// <para>・すべて 0 ("0,0,0,0,0") — ランダムと同じ指定になる。
        /// <see cref="Win5AutoMode.Random"/> を使うこと。</para>
        /// <para>・0 が 1 つも無い ("3,7,1,5,2") — 買い目が 1 通りに決まり、依頼した点数を
        /// サーバが生成できない。<see cref="BetWin5"/> で直接指定すること。</para>
        /// </param>
        /// <param name="betCount">生成させる点数 (1〜<see cref="MAX_WIN5_AUTO_BET_COUNT"/>)</param>
        /// <param name="kingaku">1 点あたりの購入金額 (円。100 円単位)</param>
        /// <param name="kaisaibi">開催日</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint BetWin5Auto(Win5AutoMode mode, string axisUmaban, uint betCount, uint kingaku, DateTime kaisaibi)
        {
            return NativeMethods.BetWin5Auto(
                (byte)mode,
                axisUmaban == null ? null : ToNullTerminatedUtf8(axisUmaban),
                (ushort)betCount, kingaku,
                (ushort)kaisaibi.Year, (byte)kaisaibi.Month, (byte)kaisaibi.Day);
        }

        /// <summary>
        /// <para>馬券購入時に残高不足が発生した場合、自動で入金してから購入に移る機能を設定する。</para>
        /// <para>入金後の残高が購入金額に満たない場合は入金を行わず
        /// <see cref="RETURN_VALUE.UNSUCCESS"/> を返す。</para>
        /// </summary>
        /// <param name="enable">true: 有効 / false: 無効</param>
        /// <param name="depositValue">
        /// 自動入金額(円・100 円単位)。enable が false の場合は検証しない。
        /// </param>
        /// <param name="confirmTimeout">
        /// 残高反映の確認タイムアウト(ms)。<see cref="Deposit"/> /
        /// <see cref="Withdraw"/> の反映待機にも使われる。
        /// </param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint SetAutoDepositFlag(bool enable, uint depositValue = DEPOSIT_DEFAULT_VALUE, ushort confirmTimeout = DEFAULT_CONFIRM_TIMEOUT)
        {
            return NativeMethods.SetAutoDepositFlag(enable, depositValue, confirmTimeout);
        }

        /// <summary>
        /// <para>指定レース・式別のオッズを取得する(<b>中央競馬・地方競馬・海外競馬</b>に対応)。</para>
        /// <para>単勝・複勝は基本オッズ、枠連〜三連単は全通りのオッズ表を取得する。</para>
        /// <para>海外開催は<b>中央競馬へのログインが必要</b>で、枠が無いため
        /// <see cref="Shikibetsu.BRACKETQUINELLA"/> を指定すると
        /// <see cref="RETURN_VALUE.UNSUCCESS"/> になる。</para>
        /// <para><see cref="Shikibetsu.WINPLACE"/>(応援馬券)はオッズの式別ではないため指定できない。</para>
        /// <para>ネイティブ側のメモリ解放はラッパー内部で行う。</para>
        /// </summary>
        /// <param name="place">開催場</param>
        /// <param name="raceNo">レース番号(1〜12)</param>
        /// <param name="shikibetsu">式別</param>
        /// <param name="oddsData">取得したオッズ情報</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint GetOdds(Kaisai place, byte raceNo, Shikibetsu shikibetsu, out ST_ODDS_DATA oddsData)
        {
            ST_ODDS_DATA_INTERNAL tempOddsData = new()
            {
                place = 0,
                raceNo = 0,
                oddsTime = new byte[8],
                detailCount = 0,
                detailData = IntPtr.Zero
            };

            uint returnValue = NativeMethods.GetOdds((ushort)place, raceNo, (byte)shikibetsu, ref tempOddsData);

            oddsData = new ST_ODDS_DATA()
            {
                place = tempOddsData.place,
                raceNo = tempOddsData.raceNo,
                oddsTime = tempOddsData.oddsTime != null
                    ? Encoding.ASCII.GetString(tempOddsData.oddsTime).TrimEnd('\0')
                    : string.Empty,
                detailCount = tempOddsData.detailCount,
                oddsDetail = Array.Empty<ST_ODDS_DETAIL>()
            };

            // 取得失敗、または明細が無い場合はここで解放して戻る
            if ((returnValue & 1) != 1 || tempOddsData.detailCount <= 0 || tempOddsData.detailData == IntPtr.Zero)
            {
                NativeMethods.ReleaseOddsData(ref tempOddsData);
                return returnValue;
            }

            // ネイティブ側で確保された明細配列をマネージド配列へ複製する
            oddsData.oddsDetail = new ST_ODDS_DETAIL[tempOddsData.detailCount];
            int detailSize = Marshal.SizeOf(typeof(ST_ODDS_DETAIL));
            for (int i = 0; i < tempOddsData.detailCount; i++)
            {
                IntPtr elementPtr = IntPtr.Add(tempOddsData.detailData, i * detailSize);
                oddsData.oddsDetail[i] = Marshal.PtrToStructure<ST_ODDS_DETAIL>(elementPtr);
            }

            // データの複製が終わったら、取得と同時にネイティブ側のメモリを解放する
            NativeMethods.ReleaseOddsData(ref tempOddsData);

            return returnValue;
        }

        /// <summary>
        /// <para>指定レースの出馬表を取得する(<b>中央競馬・地方競馬・海外競馬</b>に対応)。</para>
        /// <para>海外開催は<b>中央競馬へのログインが必要</b>で、I-PAT が返す項目が国内より少ない。
        /// 取得できるのは umaban / horseName / winPopular / 単勝・複勝オッズ、および
        /// raceName / deadline / raceStatus のみで、枠番・性齢・馬体重・騎手・斤量・調教師は
        /// 0 または空文字になる。</para>
        /// <para>ネイティブ側のメモリ解放はラッパー内部で行う。</para>
        /// </summary>
        /// <param name="place">開催場</param>
        /// <param name="raceNo">レース番号(1〜12)</param>
        /// <param name="raceCard">取得した出馬表情報</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint GetRaceCard(Kaisai place, byte raceNo, out ST_RACECARD_DATA raceCard)
        {
            ST_RACECARD_DATA_INTERNAL tempRaceCardData = new()
            {
                usPlace = 0,
                ucRaceNo = 0,
                szOddsTime = new byte[8],
                unEntryCount = 0,
                pobjEntry = IntPtr.Zero,
                szRaceName = new byte[128],
                szDeadline = new byte[8],
                ucRaceStatus = (byte)RACE_STATUS.UNKNOWN
            };

            uint returnValue = NativeMethods.GetRaceCard((ushort)place, raceNo, ref tempRaceCardData);

            raceCard = new ST_RACECARD_DATA()
            {
                place = tempRaceCardData.usPlace,
                raceNo = tempRaceCardData.ucRaceNo,
                oddsTime = DecodeUtf8(tempRaceCardData.szOddsTime),
                entryCount = tempRaceCardData.unEntryCount,
                entries = Array.Empty<ST_ENTRY_DETAIL>(),
                raceName = DecodeUtf8(tempRaceCardData.szRaceName),
                deadline = DecodeUtf8(tempRaceCardData.szDeadline),
                raceStatus = (RACE_STATUS)tempRaceCardData.ucRaceStatus
            };

            // 取得失敗、または明細が無い場合はここで解放して戻る
            if ((returnValue & 1) != 1 || tempRaceCardData.unEntryCount <= 0 || tempRaceCardData.pobjEntry == IntPtr.Zero)
            {
                NativeMethods.ReleaseRaceCardData(ref tempRaceCardData);
                return returnValue;
            }

            // ネイティブ側で確保された明細配列をマネージド配列へ複製する
            raceCard.entries = new ST_ENTRY_DETAIL[tempRaceCardData.unEntryCount];
            int entrySize = Marshal.SizeOf(typeof(ST_ENTRY_DETAIL_INTERNAL));
            for (int i = 0; i < tempRaceCardData.unEntryCount; i++)
            {
                IntPtr elementPtr = IntPtr.Add(tempRaceCardData.pobjEntry, i * entrySize);
                ST_ENTRY_DETAIL_INTERNAL e = Marshal.PtrToStructure<ST_ENTRY_DETAIL_INTERNAL>(elementPtr);

                raceCard.entries[i] = new ST_ENTRY_DETAIL()
                {
                    wakuban = e.ucWakuban,
                    umaban = e.ucUmaban,
                    horseName = DecodeUtf8(e.szHorseName),
                    sex = DecodeUtf8(e.szSex),
                    age = e.ucAge,
                    weightStatus = e.ucWeightStatus,
                    weight = e.usWeight,
                    weightDiffCode = e.ucWeightDiffCode,
                    weightDiff = e.usWeightDiff,
                    apprentice = e.ucApprentice,
                    jockeyName = DecodeUtf8(e.szJockeyName),
                    burden = e.usBurden,
                    trainerName = DecodeUtf8(e.szTrainerName),
                    winPopular = e.usWinPopular,
                    winOddsStatus = e.ucWinOddsStatus,
                    winOdds = e.unWinOdds,
                    placeOddsStatus = e.ucPlaceOddsStatus,
                    placeOddsLow = e.unPlaceOddsLow,
                    placeOddsHigh = e.unPlaceOddsHigh
                };
            }

            // データの複製が終わったら、取得と同時にネイティブ側のメモリを解放する
            NativeMethods.ReleaseRaceCardData(ref tempRaceCardData);

            return returnValue;
        }

        /// <summary>
        /// お知らせ取得処理実行
        /// </summary>
        /// <param name="notice">お知らせ情報(強制表示本文＋お知らせ一覧)</param>
        /// <returns></returns>
        public static uint GetNotice(out ST_NOTICE_DATA notice)
        {
            ST_NOTICE_DATA_INTERNAL tempNoticeData = new()
            {
                szMessage = new byte[2048],
                szNoticeNo = new byte[16],
                szNoticeType = new byte[8],
                unItemCount = 0,
                pobjItem = IntPtr.Zero
            };

            uint returnValue = NativeMethods.GetNotice(ref tempNoticeData);

            notice = new ST_NOTICE_DATA()
            {
                message = DecodeUtf8(tempNoticeData.szMessage),
                noticeNo = DecodeUtf8(tempNoticeData.szNoticeNo),
                noticeType = DecodeUtf8(tempNoticeData.szNoticeType),
                itemCount = tempNoticeData.unItemCount,
                items = Array.Empty<ST_NOTICE_ITEM>()
            };

            // 取得失敗、または一覧が無い場合はここで解放して戻る
            if ((returnValue & 1) != 1 || tempNoticeData.unItemCount <= 0 || tempNoticeData.pobjItem == IntPtr.Zero)
            {
                NativeMethods.ReleaseNoticeData(ref tempNoticeData);
                return returnValue;
            }

            // ネイティブ側で確保された一覧配列をマネージド配列へ複製する
            notice.items = new ST_NOTICE_ITEM[tempNoticeData.unItemCount];
            int itemSize = Marshal.SizeOf(typeof(ST_NOTICE_ITEM_INTERNAL));
            for (int i = 0; i < tempNoticeData.unItemCount; i++)
            {
                IntPtr elementPtr = IntPtr.Add(tempNoticeData.pobjItem, i * itemSize);
                ST_NOTICE_ITEM_INTERNAL it = Marshal.PtrToStructure<ST_NOTICE_ITEM_INTERNAL>(elementPtr);

                notice.items[i] = new ST_NOTICE_ITEM()
                {
                    title = DecodeUtf8(it.szTitle),
                    date = DecodeUtf8(it.szDate),
                    url = DecodeUtf8(it.szUrl),
                    icon = DecodeUtf8(it.szIcon),
                    color = DecodeUtf8(it.szColor)
                };
            }

            // データの複製が終わったら、取得と同時にネイティブ側のメモリを解放する
            NativeMethods.ReleaseNoticeData(ref tempNoticeData);

            return returnValue;
        }

        /// <summary>
        /// <para>本日開催されている開催場の一覧を取得する。</para>
        /// <para>開催場ごとに、レース番号・発売締切時刻・発売状態・レース名も併せて返す。</para>
        /// <para>ログイン済みの系統(中央・地方)と、中央にログインしていれば海外を対象とする。
        /// <b>系統ごとに1回ずつ、最大3回の通信で全開催場が得られる</b>ため、
        /// 「どの開催場が開催中か」を調べるために <see cref="GetRaceCard"/> を
        /// 開催場の数だけ呼ぶ必要はない。</para>
        /// <para>片方の系統だけ失敗した場合は、取得できた分を返したうえで
        /// <see cref="RETURN_VALUE.FAILED_CHUOU"/> / <see cref="RETURN_VALUE.FAILED_CHIHOU"/>
        /// を立てる(<see cref="RETURN_VALUE.SUCCESS"/> と同時に立つ)。</para>
        /// <para>開催が1つも無い場合は kaisaiCount が 0 で成功を返す。</para>
        /// <para>ネイティブ側のメモリ解放はラッパー内部で行う。</para>
        /// </summary>
        /// <param name="kaisaiData">取得した開催場一覧</param>
        /// <returns><see cref="RETURN_VALUE"/> のビットフラグ</returns>
        public static uint GetKaisaiList(out ST_KAISAI_DATA kaisaiData)
        {
            ST_KAISAI_DATA_INTERNAL tempKaisaiData = new()
            {
                unKaisaiCount = 0,
                pobjKaisai = IntPtr.Zero
            };

            uint returnValue = NativeMethods.GetKaisaiList(ref tempKaisaiData);

            kaisaiData = new ST_KAISAI_DATA()
            {
                kaisaiCount = tempKaisaiData.unKaisaiCount,
                kaisai = Array.Empty<ST_KAISAI_ITEM>()
            };

            // 取得失敗、または開催が無い場合はここで解放して戻る
            if ((returnValue & 1) != 1 || tempKaisaiData.unKaisaiCount <= 0 || tempKaisaiData.pobjKaisai == IntPtr.Zero)
            {
                kaisaiData.kaisaiCount = 0;
                NativeMethods.ReleaseKaisaiData(ref tempKaisaiData);
                return returnValue;
            }

            // ネイティブ側で確保された配列をマネージド配列へ複製する
            kaisaiData.kaisai = new ST_KAISAI_ITEM[tempKaisaiData.unKaisaiCount];
            int itemSize = Marshal.SizeOf(typeof(ST_KAISAI_ITEM_INTERNAL));
            int raceSize = Marshal.SizeOf(typeof(ST_KAISAI_RACE_INTERNAL));
            for (int i = 0; i < tempKaisaiData.unKaisaiCount; i++)
            {
                IntPtr itemPtr = IntPtr.Add(tempKaisaiData.pobjKaisai, i * itemSize);
                ST_KAISAI_ITEM_INTERNAL it = Marshal.PtrToStructure<ST_KAISAI_ITEM_INTERNAL>(itemPtr);

                var races = Array.Empty<ST_KAISAI_RACE>();
                if (it.unRaceCount > 0 && it.pobjRace != IntPtr.Zero)
                {
                    races = new ST_KAISAI_RACE[it.unRaceCount];
                    for (int r = 0; r < it.unRaceCount; r++)
                    {
                        IntPtr racePtr = IntPtr.Add(it.pobjRace, r * raceSize);
                        ST_KAISAI_RACE_INTERNAL rc = Marshal.PtrToStructure<ST_KAISAI_RACE_INTERNAL>(racePtr);

                        races[r] = new ST_KAISAI_RACE()
                        {
                            raceNo = rc.ucRaceNo,
                            raceStatus = (RACE_STATUS)rc.ucRaceStatus,
                            deadline = DecodeUtf8(rc.szDeadline),
                            raceName = DecodeUtf8(rc.szRaceName)
                        };
                    }
                }

                kaisaiData.kaisai[i] = new ST_KAISAI_ITEM()
                {
                    place = (Kaisai)it.usPlace,
                    raceCount = (uint)races.Length,
                    races = races
                };
            }

            // データの複製が終わったら、取得と同時にネイティブ側のメモリを解放する
            NativeMethods.ReleaseKaisaiData(ref tempKaisaiData);

            return returnValue;
        }

        /// <summary>
        /// <para>文字列を UTF-8 の null 終端バイト列へ変換する。</para>
        /// <para>DLL 側は <c>const char[]</c> を null 終端として読むが、
        /// byte 配列のマーシャリングは終端を付けないため、ここで明示的に付ける。
        /// (付けないと DLL が確保領域の外まで読み進める。)</para>
        /// </summary>
        private static byte[] ToNullTerminatedUtf8(string value)
        {
            value ??= string.Empty;

            int byteCount = Encoding.UTF8.GetByteCount(value);
            byte[] buffer = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
            buffer[byteCount] = 0;
            return buffer;
        }

        /// <summary>
        /// DLLが返すUTF-8・null終端のバイト列を文字列へデコードする
        /// </summary>
        private static string DecodeUtf8(byte[] raw)
        {
            if (raw == null)
            {
                return string.Empty;
            }

            int length = Array.IndexOf(raw, (byte)0);
            if (length < 0)
            {
                length = raw.Length;
            }

            return length == 0 ? string.Empty : Encoding.UTF8.GetString(raw, 0, length);
        }
        #endregion
    }
}