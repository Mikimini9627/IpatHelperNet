# IpatHelperNet

[![NuGet](https://img.shields.io/nuget/v/IpatHelperNet.svg)](https://www.nuget.org/packages/IpatHelperNet)
[![NuGet Downloads](https://img.shields.io/nuget/dt/IpatHelperNet.svg)](https://www.nuget.org/packages/IpatHelperNet)

JRA I-PAT（インターネット投票）への馬券購入・入出金・購入履歴取得・オッズ取得・出馬表取得を自動化する Windows 用 C# ラッパーライブラリです。
内部でネイティブ DLL（`IpatHelper.dll`）を P/Invoke 経由で呼び出しており、中央競馬・地方競馬・海外競馬・WIN5 に対応しています。

> **重要:** すべての API は `IpatHelper` クラスの **静的メソッド**として提供され、戻り値は **`uint`（ビットフラグ）** です。
> インスタンス化や `using`（`IDisposable`）は不要・非対応です。判定は `RETURN_VALUE` 定数との AND 演算で行います。

---

## 動作環境

| 項目 | 内容 |
|---|---|
| OS | Windows 10 以降（64bit / 32bit） |
| .NET | .NET 8.0 / 9.0 / 10.0 |
| アーキテクチャ | x64 / x86（`Platform` を明示してください。AnyCPU は不可） |

> ネイティブ DLL（`IpatHelper.dll`）は NuGet パッケージに同梱され、ビルド時に自動で出力フォルダへ配置されます。追加のインストールは不要です。

---

## インストール

### .NET CLI

```bash
dotnet add package IpatHelperNet
```

### Package Manager Console

```powershell
Install-Package IpatHelperNet
```

### .csproj に直接記述

```xml
<PackageReference Include="IpatHelperNet" Version="1.1.0" />
```

> **プラットフォーム指定:** ネイティブ DLL に合わせて、プロジェクトの `Platform` を `x64` または `x86` に設定してください。

```xml
<PropertyGroup>
  <Platforms>x64;x86</Platforms>
  <Platform Condition="'$(Platform)' == 'AnyCPU'">x64</Platform>
</PropertyGroup>
```

---

## クイックスタート

```csharp
using IpatHelperNet;

// 1. ログイン（各自の認証情報に置き換えてください）
uint ret = IpatHelper.Login("1234567890", "12345678", "1234", "12345");
if ((ret & (uint)IpatHelper.RETURN_VALUE.SUCCESS) == 0)
{
    Console.WriteLine("ログイン失敗");
    return;
}

// 2. 馬券購入情報を構築（東京 11R、単勝 1番、100円）
ret = IpatHelper.GetBetInstance(
    IpatHelper.Kaisai.TOKYO, raceNo: 11,
    kaisaibi: new DateTime(2025, 5, 25),
    IpatHelper.Houshiki.NORMAL, IpatHelper.Shikibetsu.WIN,
    kingaku: 100, kaime: "1",
    out IpatHelper.ST_BET_DATA betData);

// 3. 購入
if ((ret & 1) == 1)
{
    IpatHelper.Bet(new() { betData });
}

// 4. ログアウト
IpatHelper.Logout();
```

> **成功判定のイディオム:** `SUCCESS` はビット 0（値 1）なので `(ret & 1) == 1` でも判定できます。
> 明示したい場合は `(ret & (uint)IpatHelper.RETURN_VALUE.SUCCESS) != 0` を使ってください。

---

## API リファレンス

すべて `IpatHelper` クラスの静的メソッドです。戻り値は `uint`（`RETURN_VALUE` のビットフラグ）です。

### Login — ログイン

I-PAT へログインします。中央競馬と地方競馬へ並列でログインを試みます。
他の全ての API を呼び出す前に必ず実行してください。

```csharp
static uint Login(string iNetId, string id, string password, string pars)
```

| 引数 | 説明 |
|---|---|
| `iNetId` | I-NET ID |
| `id` | ログイン ID（加入者番号） |
| `password` | パスワード |
| `pars` | P-ARS 番号 |

```csharp
uint ret = IpatHelper.Login("1234567890", "12345678", "1234", "12345");

if ((ret & (uint)IpatHelper.RETURN_VALUE.SUCCESS) != 0)      Console.WriteLine("ログイン成功");
if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_CHUOU) != 0) Console.WriteLine("中央競馬のログインに失敗");
if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_CHIHOU) != 0) Console.WriteLine("地方競馬のログインに失敗");
```

> 中央・地方のどちらか一方でも成功すれば `SUCCESS` が立ちます。失敗した系統のみ購入できません。

---

### Logout — ログアウト

I-PAT からログアウトし、内部セッション情報・自動入金設定を初期化します。

```csharp
static uint Logout()
```

```csharp
IpatHelper.Logout();
```

---

### Deposit — 入金

登録口座から I-PAT 口座へ入金します。

```csharp
static uint Deposit(uint depositValue, ushort retryCount = 10)
```

| 引数 | 説明 |
|---|---|
| `depositValue` | 入金額（円・100円単位） |
| `retryCount` | 通信失敗時のリトライ回数（デフォルト: 10） |

```csharp
uint ret = IpatHelper.Deposit(10000);  // 10,000円 入金
```

> **注意:** 入金額は100円以上かつ100円単位で指定してください。条件を満たさない場合は `UNSUCCESS` が返ります。

---

### Withdraw — 出金

I-PAT 口座から登録口座へ全額出金します。

```csharp
static uint Withdraw(ushort retryCount = 10)
```

```csharp
uint ret = IpatHelper.Withdraw();
```

---

### SetAutoDepositFlag — 自動入金設定

馬券購入時に残高不足が発生した場合、自動で入金を行う機能を設定します。

```csharp
static uint SetAutoDepositFlag(bool enable, uint depositValue = 1000, ushort confirmTimeout = 10000)
```

| 引数 | 説明 |
|---|---|
| `enable` | `true`: 有効 / `false`: 無効 |
| `depositValue` | 自動入金額（円・100円単位、デフォルト: 1000円） |
| `confirmTimeout` | 入金反映確認タイムアウト（ミリ秒、デフォルト: 10000ms） |

```csharp
// 残高不足時に自動で 5,000円 入金（反映確認タイムアウト 15秒）
IpatHelper.SetAutoDepositFlag(true, 5000, 15000);

// 無効化
IpatHelper.SetAutoDepositFlag(false);
```

---

### GetBetInstance — 馬券購入情報の構築

買い目文字列から `ST_BET_DATA` を構築します。`Bet` を呼び出す前に必ずこの関数で購入情報を生成してください。

```csharp
static uint GetBetInstance(
    Kaisai place, byte raceNo,
    DateTime kaisaibi,
    Houshiki houshiki, Shikibetsu shikibetsu,
    uint kingaku, string kaime,
    out ST_BET_DATA betData)
```

| 引数 | 説明 |
|---|---|
| `place` | 開催場（`Kaisai` 列挙値） |
| `raceNo` | レース番号（1〜12） |
| `kaisaibi` | 開催日（`DateTime`） |
| `houshiki` | 方式（`Houshiki` 列挙値） |
| `shikibetsu` | 式別（`Shikibetsu` 列挙値） |
| `kingaku` | 1点あたりの購入金額（100円単位） |
| `kaime` | 買い目文字列（後述） |
| `betData` | 出力: 構築された購入情報 |

```csharp
// 東京 11R、三連単フォーメーション、1,2 → 3 → 4,5、200円
uint ret = IpatHelper.GetBetInstance(
    IpatHelper.Kaisai.TOKYO, 11,
    new DateTime(2025, 5, 25),
    IpatHelper.Houshiki.FORMATION, IpatHelper.Shikibetsu.TRIFECTA,
    200, "1,2-3-4,5",
    out IpatHelper.ST_BET_DATA betData);
```

---

### Bet — 馬券購入

`GetBetInstance` で生成した `ST_BET_DATA` のリストを渡して馬券を購入します。
異なる開催場の買い目も一括で渡せます（中央・地方・海外を自動振り分け）。

```csharp
static uint Bet(List<ST_BET_DATA> betDataList, ushort waitMiliSeconds = 1000)
```

| 引数 | 説明 |
|---|---|
| `betDataList` | 購入情報のリスト |
| `waitMiliSeconds` | 購入リクエスト間隔（ミリ秒、デフォルト: 1000ms） |

```csharp
// 単一購入
IpatHelper.Bet(new() { betData });

// 複数まとめて購入
var bets = new List<IpatHelper.ST_BET_DATA>();
IpatHelper.GetBetInstance(IpatHelper.Kaisai.TOKYO,    11, new DateTime(2025, 5, 25), IpatHelper.Houshiki.NORMAL, IpatHelper.Shikibetsu.WIN,      100, "1",     out var b0);
IpatHelper.GetBetInstance(IpatHelper.Kaisai.NAKAYAMA,  9, new DateTime(2025, 5, 25), IpatHelper.Houshiki.BOX,    IpatHelper.Shikibetsu.TRIFECTA, 100, "1,3,5", out var b1);
IpatHelper.GetBetInstance(IpatHelper.Kaisai.OI,        7, new DateTime(2025, 5, 25), IpatHelper.Houshiki.NORMAL, IpatHelper.Shikibetsu.WIN,      100, "2",     out var b2);
bets.Add(b0); bets.Add(b1); bets.Add(b2);

IpatHelper.Bet(bets);
```

---

### GetBetInstanceWin5 / BetWin5 — WIN5 購入

WIN5 の買い目文字列から購入情報を構築し、購入します。**WIN5 は中央競馬でのみ購入可能**です。

```csharp
static uint GetBetInstanceWin5(uint kingaku, DateTime kaisaibi, string kaime, out ST_BET_DATA_WIN5 betData)
static uint BetWin5(ST_BET_DATA_WIN5 betData, ushort waitMiliSeconds = 1000)
```

```csharp
// 5レース分の買い目（各レースをハイフン区切り、複数頭はカンマ区切り）
uint ret = IpatHelper.GetBetInstanceWin5(
    100, new DateTime(2025, 5, 25),
    "1,2-3-4,5-2,6-1",
    out IpatHelper.ST_BET_DATA_WIN5 win5Data);

if ((ret & 1) == 1)
{
    IpatHelper.BetWin5(win5Data);
}
```

---

### GetPurchaseData — 購入履歴取得

当日・前日の馬券購入履歴、残高、購入可能件数を取得します。メモリ解放はラッパー内部で行うため、呼び出し側での解放は不要です。

```csharp
static uint GetPurchaseData(out ST_PURCHASE_DATA purchaseData)
```

```csharp
uint ret = IpatHelper.GetPurchaseData(out var data);
if ((ret & 1) == 1)
{
    Console.WriteLine($"残高: {data.balance} 円");
    Console.WriteLine($"当日購入: {data.dayPurchase} 円");
    Console.WriteLine($"当日払戻: {data.dayHaraimodosi} 円");
    Console.WriteLine($"馬券件数: {data.ticketCount} 件");

    foreach (var ticket in data.ticketData)
    {
        Console.WriteLine($"受付No.{ticket.receiptNo} {ticket.hour:00}:{ticket.minute:00} " +
                          $"{ticket.kingaku}円 (払戻 {ticket.payout}円)");
    }
}
```

---

### GetOdds — オッズ取得

指定レース・式別のオッズを取得します（**中央競馬・地方競馬**に対応）。単勝・複勝は基本オッズ、枠連〜三連単は全通りのオッズ表を取得します。

```csharp
static uint GetOdds(Kaisai place, byte raceNo, Shikibetsu shikibetsu, out ST_ODDS_DATA oddsData)
```

- ネイティブ側で確保されたメモリはラッパー内部で解放するため、呼び出し側での解放は不要です。
- オッズは 10 倍の整数（`odds`）で格納されます（例: 12.3 倍 → `123`）。実際の倍率は `odds / 10.0`。
- 複勝・ワイドは下限を `odds`、上限を `oddsHigh` に格納します。
- `status` が `1`（発売中止）／`2`（オッズ未取得）の場合、`odds` / `oddsHigh` は `0` です。

```csharp
uint ret = IpatHelper.GetOdds(IpatHelper.Kaisai.TOKYO, 11, IpatHelper.Shikibetsu.QUINELLA, out var odds);
if ((ret & 1) == 1)
{
    Console.WriteLine($"オッズ更新時刻: {odds.oddsTime} / 明細数: {odds.detailCount}");
    foreach (var d in odds.oddsDetail)
    {
        string kaime = d.horse1.ToString();
        if (d.horse2 != 0) kaime += "-" + d.horse2;
        if (d.horse3 != 0) kaime += "-" + d.horse3;
        string oddsText = d.status == 0 ? (d.odds / 10.0).ToString("0.0") : "-";
        Console.WriteLine($"{kaime} : {oddsText}");
    }
}
```

`ST_ODDS_DATA` / `ST_ODDS_DETAIL` の各フィールド:

| 構造体 | フィールド | 内容 |
|---|---|---|
| `ST_ODDS_DATA` | `place` / `raceNo` | 開催場 / レース番号 |
| | `oddsTime` | オッズ更新時刻 "HH:MM" |
| | `detailCount` / `oddsDetail` | 明細数 / 明細配列 |
| `ST_ODDS_DETAIL` | `type` | 式別（Shikibetsu） |
| | `horse1` / `horse2` / `horse3` | 馬番/枠番（単複は1頭、馬連・ワイド・馬単・枠連は2頭、三連系は3頭） |
| | `status` | 0:通常 1:発売中止 2:オッズ未取得 |
| | `odds` / `oddsHigh` | オッズ×10（複勝・ワイドは下限/上限） |

> **式別と明細数の目安（N 頭立て）:** 単勝/複勝 N点、馬連/ワイド N×(N−1)/2点、馬単 N×(N−1)点、三連複 N×(N−1)×(N−2)/6点、三連単 N×(N−1)×(N−2)点。開催場によって発売のない式別（地方の枠連など）は明細0件またはサーバーエラーになります。

---

### GetRaceCard — 出馬表取得

指定レースの出馬表（出走馬一覧）を取得します（**中央競馬・地方競馬**に対応）。各出走馬の枠番・馬番・馬名・性齢・馬体重・騎手・斤量・調教師・単勝人気・単勝/複勝オッズを取得します。

```csharp
static uint GetRaceCard(Kaisai place, byte raceNo, out ST_RACECARD_DATA raceCard)
```

- ネイティブ側で確保されたメモリはラッパー内部で解放するため、呼び出し側での解放は不要です。
- 文字列（馬名・騎手名・調教師名など）は UTF-8 からデコード済みの `string` として格納されます。
- 斤量・オッズは 10 倍の整数で格納されます。実際の値は `/ 10.0`。

```csharp
uint ret = IpatHelper.GetRaceCard(IpatHelper.Kaisai.TOKYO, 11, out var raceCard);
if ((ret & 1) == 1)
{
    Console.WriteLine($"オッズ更新時刻: {raceCard.oddsTime} / 出走頭数: {raceCard.entryCount}");
    foreach (var e in raceCard.entries)
    {
        string win = e.winOddsStatus == 0 ? (e.winOdds / 10.0).ToString("0.0") : "-";
        Console.WriteLine($"{e.umaban,2}番 {e.horseName} {e.sex}{e.age} " +
                          $"斤量{e.burden / 10.0:0.0} 騎手:{e.jockeyName} 単勝:{win} 人気:{e.winPopular}");
    }
}
```

`ST_RACECARD_DATA` / `ST_ENTRY_DETAIL` の各フィールド:

| 構造体 | フィールド | 内容 |
|---|---|---|
| `ST_RACECARD_DATA` | `place` / `raceNo` | 開催場 / レース番号 |
| | `oddsTime` | オッズ更新時刻 "HH:MM" |
| | `entryCount` / `entries` | 出走馬数 / 出走馬明細配列 |
| `ST_ENTRY_DETAIL` | `wakuban` / `umaban` | 枠番 / 馬番 |
| | `horseName` / `sex` / `age` | 馬名 / 性別 / 年齢 |
| | `weightStatus` / `weight` / `weightDiffCode` / `weightDiff` | 馬体重の状態・重量(kg)・増減符号・増減量 |
| | `apprentice` | 見習騎手コード(0:なし 1〜5:減量 9:女性騎手2kg減) |
| | `jockeyName` / `burden` / `trainerName` | 騎手名 / 斤量×10 / 調教師名 |
| | `winPopular` | 単勝人気(0:データなし) |
| | `winOddsStatus` / `winOdds` | 単勝オッズの状態(0:通常 1:発売中止 2:未取得) / オッズ×10 |
| | `placeOddsStatus` / `placeOddsLow` / `placeOddsHigh` | 複勝オッズの状態 / 下限×10 / 上限×10 |

> **補足:** `winOddsStatus` が `1`（発売中止）の馬は出走取消・競走除外の可能性があります。`weightStatus` が `2` の場合も出走取消です。

---

## 買い目文字列の書式

`GetBetInstance` および `GetBetInstanceWin5` に渡す買い目文字列のフォーマットです。

- 列の区切り: **ハイフン（`-`）**
- 同一列内の複数馬番: **カンマ（`,`）**

### 方式別の例

| 方式 | 式別 | 買い目文字列 | 説明 |
|---|---|---|---|
| 通常 | 単勝 | `"1"` | 1番 |
| 通常 | 馬連 | `"1-5"` | 1番 - 5番 |
| 通常 | 三連単 | `"1-3-5"` | 1着1番・2着3番・3着5番 |
| フォーメーション | 馬連 | `"1,2-3,4,5"` | 1,2番 から 3,4,5番 |
| フォーメーション | 三連単 | `"1,2-3-4,5"` | 1,2番 → 3番 → 4,5番 |
| ボックス | 馬連 | `"1,3,5,7"` | 1,3,5,7番 の全組み合わせ |
| ボックス | 三連単 | `"2,4,6"` | 2,4,6番 の全組み合わせ |

### WIN5 の例

| 買い目文字列 | 説明 |
|---|---|
| `"1-2-3-4-5"` | 各レース1頭ずつ指定 |
| `"1,2-3-4,5-2,6-1"` | 一部のレースで複数頭指定 |

---

## 列挙型

### Kaisai（開催場）

#### 中央競馬
`SAPPORO`（札幌）/ `HAKODATE`（函館）/ `FUKUSHIMA`（福島）/ `NIIGATA`（新潟）/ `TOKYO`（東京）/ `NAKAYAMA`（中山）/ `CHUKYO`（中京）/ `KYOTO`（京都）/ `HANSHIN`（阪神）/ `KOKURA`（小倉）

#### 地方競馬
`SONODA`（園田）/ `HIMEJI`（姫路）/ `NAGOYA`（名古屋）/ `MONBETSU`（門別）/ `MORIOKA`（盛岡）/ `MIZUSAWA`（水沢）/ `URAWA`（浦和）/ `FUNABASHI`（船橋）/ `OI`（大井）/ `KAWASAKI`（川崎）/ `KASAMATSU`（笠松）/ `KANAZAWA`（金沢）/ `KOCHI`（高知）/ `SAGA`（佐賀）

#### 海外競馬
`LONGCHAMP`（ロンシャン）/ `SHATIN`（シャティン）/ `SANTAANITA`（サンタアニタ）/ `DEAUVILE`（ドーヴィル）/ `CHURCHILLDOWNS`（チャーチルダウンズ）/ `ABDULAZIZ`（キングアブドゥルアジーズ）

### Shikibetsu（式別）

| 値 | 式別 |
|---|---|
| `WIN` | 単勝 |
| `PLACE` | 複勝 |
| `BRACKETQUINELLA` | 枠連 |
| `QUINELLA` | 馬連 |
| `QUINELLAPLACE` | ワイド |
| `EXACTA` | 馬単 |
| `TRIO` | 三連複 |
| `TRIFECTA` | 三連単 |

### Houshiki（方式）

| 値 | 方式 |
|---|---|
| `NORMAL` | 通常 |
| `FORMATION` | フォーメーション |
| `BOX` | ボックス |

### RETURN_VALUE（戻り値ビットフラグ）

各 API は `uint` を返します。以下のフラグとの AND 演算で判定してください（複数同時に立つ場合があります）。

| 定数 | 値 | 意味 |
|---|---|---|
| `SUCCESS` | 1 | 処理に成功 |
| `UNSUCCESS` | 2 | 処理に失敗（パラメータ不正・残高不足・未ログイン等） |
| `FAILED_CHUOU` | 4 | 中央競馬での処理に失敗 |
| `FAILED_CHIHOU` | 8 | 地方競馬での処理に失敗 |
| `FAILED_COMMUNICATE_CHUOU` | 16 | 中央競馬との通信に失敗 |
| `FAILED_COMMUNICATE_CHIHOU` | 32 | 地方競馬との通信に失敗 |

```csharp
uint ret = IpatHelper.Bet(new() { betData });

if ((ret & (uint)IpatHelper.RETURN_VALUE.SUCCESS) != 0)                 Console.WriteLine("購入成功");
if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_CHUOU) != 0)            Console.WriteLine("中央競馬での処理に失敗");
if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_COMMUNICATE_CHUOU) != 0) Console.WriteLine("中央競馬との通信に失敗");
```

---

## 総合的な使用例

```csharp
using IpatHelperNet;

// ログイン
uint ret = IpatHelper.Login("1234567890", "12345678", "1234", "12345");
if ((ret & 1) != 1)
{
    Console.WriteLine("ログイン失敗");
    return;
}

try
{
    // 残高不足時に自動で 10,000円 入金
    IpatHelper.SetAutoDepositFlag(true, 10000);

    // オッズを確認（東京11R 馬連）
    if ((IpatHelper.GetOdds(IpatHelper.Kaisai.TOKYO, 11, IpatHelper.Shikibetsu.QUINELLA, out var odds) & 1) == 1)
    {
        Console.WriteLine($"オッズ更新: {odds.oddsTime} / {odds.detailCount}点");
    }

    // 出馬表を確認
    if ((IpatHelper.GetRaceCard(IpatHelper.Kaisai.TOKYO, 11, out var card) & 1) == 1)
    {
        Console.WriteLine($"{card.entryCount}頭立て");
    }

    // 購入情報を構築
    IpatHelper.GetBetInstance(
        IpatHelper.Kaisai.TOKYO, 11, new DateTime(2025, 5, 25),
        IpatHelper.Houshiki.FORMATION, IpatHelper.Shikibetsu.QUINELLA,
        200, "1,2-3,4,5", out var betData);

    // 購入
    if ((IpatHelper.Bet(new() { betData }) & 1) == 1)
    {
        Console.WriteLine("購入成功");
    }

    // 購入履歴を確認
    if ((IpatHelper.GetPurchaseData(out var history) & 1) == 1)
    {
        Console.WriteLine($"残高: {history.balance} 円 / 当日購入: {history.dayPurchase} 円");
    }
}
finally
{
    // 必ずログアウト
    IpatHelper.Logout();
}
```

---

## トラブルシューティング

| 症状 | 原因 / 対処 |
|---|---|
| `DllNotFoundException: IpatHelper.dll` | プロジェクトの `Platform` を `x64` または `x86` に設定してください（AnyCPU は不可）。NuGet 同梱の DLL が出力フォルダに配置されているか確認します。 |
| `EntryPointNotFoundException: GetRaceCard` 等 | 出力フォルダの `IpatHelper.dll` が旧版です。パッケージを最新（1.1.0 以降）に更新するか、`bin` を削除して再ビルドしてください。 |
| ログインは成功するが購入で `FAILED_CHUOU` | 中央競馬にログインできていない可能性があります。`Login` の戻り値で `FAILED_CHUOU` を確認してください。 |
| オッズ/出馬表が `UNSUCCESS` | 指定した開催場が当日開催されていない、または海外開催を指定しています（オッズ・出馬表は海外非対応）。 |
| 文字化けする | 文字列は UTF-8 デコード済みで返ります。コンソール出力時は `Console.OutputEncoding = Encoding.UTF8;` を設定してください。 |

---

## 注意事項

- 本ライブラリは **Windows 専用**です。
- 本ライブラリを使用した馬券購入は**実際の金銭を伴います**。十分にテストしてからご使用ください。
- I-PAT の仕様変更により動作しなくなる場合があります。
- ログイン情報はメモリ上にのみ保持され、ファイルへの保存は行いません。
- 全 API はスレッドセーフです（内部でクリティカルセクションによる排他制御を実施）。

---

## ライセンス

MIT License
