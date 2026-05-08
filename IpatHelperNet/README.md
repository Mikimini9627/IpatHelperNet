# IpatHelperNet

[![NuGet](https://img.shields.io/nuget/v/IpatHelperNet.svg)](https://www.nuget.org/packages/IpatHelperNet)
[![NuGet Downloads](https://img.shields.io/nuget/dt/IpatHelperNet.svg)](https://www.nuget.org/packages/IpatHelperNet)

JRA I-PAT（インターネット投票）への馬券購入・入出金・購入履歴取得を自動化する Windows 用 C# ラッパーライブラリです。  
内部でネイティブ DLL（`IpatHelper.dll`）を P/Invoke 経由で呼び出しており、中央競馬・地方競馬・海外競馬・WIN5 に対応しています。

---

## 動作環境

| 項目 | 内容 |
|---|---|
| OS | Windows 10 以降（64bit / 32bit） |
| .NET | .NET 6.0 以降 |
| アーキテクチャ | x64 / x86 |

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
<PackageReference Include="IpatHelperNet" Version="1.0.0" />
```

---

## クイックスタート

```csharp
using IpatHelperNet;

using var ipat = new IpatHelper();

// 1. ログイン
var result = ipat.Login("1234567890", "12345678", "1234", "12345");
if (!result.HasFlag(RETURN_VALUE.SUCCESS))
{
    Console.WriteLine("ログイン失敗");
    return;
}

// 2. 馬券購入情報を構築（東京 11R、単勝 1番、100円）
var ret = ipat.GetBetInstance(
    KAISAI.TOKYO, raceNo: 11,
    year: 2025, month: 5, day: 25,
    HOUSHIKI.NORMAL, SHIKIBETSU.WIN,
    amount: 100, kaime: "1",
    out var betData);

// 3. 購入
if (ret.HasFlag(RETURN_VALUE.SUCCESS))
{
    ipat.Bet(new[] { betData });
}

// 4. ログアウト（using による自動呼び出しも可）
ipat.Logout();
```

---

## API リファレンス

### Login — ログイン

I-PAT へログインします。中央競馬と地方競馬へ並列でログインを試みます。  
他の全ての API を呼び出す前に必ず実行してください。

```csharp
RETURN_VALUE Login(string inetId, string id, string password, string pars)
```

| 引数 | 説明 |
|---|---|
| `inetId` | I-NET ID |
| `id` | ログイン ID |
| `password` | パスワード |
| `pars` | P-ARS 番号 |

```csharp
var result = ipat.Login("1234567890", "12345678", "1234", "12345");

if (result.HasFlag(RETURN_VALUE.SUCCESS))
{
    Console.WriteLine("ログイン成功");
}
if (result.HasFlag(RETURN_VALUE.FAILED_CHUOU))
{
    Console.WriteLine("中央競馬のログインに失敗");
}
if (result.HasFlag(RETURN_VALUE.FAILED_CHIHOU))
{
    Console.WriteLine("地方競馬のログインに失敗");
}
```

---

### Logout — ログアウト

I-PAT からログアウトし、内部セッション情報を初期化します。  
`IpatHelper` は `IDisposable` を実装しており、`using` を使うと自動でログアウトされます。

```csharp
RETURN_VALUE Logout()
```

```csharp
// 明示的にログアウト
ipat.Logout();

// using を使った自動ログアウト（推奨）
using var ipat = new IpatHelper();
```

---

### Deposit — 入金

登録口座から I-PAT 口座へ入金します。

```csharp
RETURN_VALUE Deposit(uint amount, ushort retryCount = 10)
```

| 引数 | 説明 |
|---|---|
| `amount` | 入金額（円・100円単位） |
| `retryCount` | 失敗時のリトライ回数（デフォルト: 10） |

```csharp
// 10,000円 入金
var result = ipat.Deposit(10000);
```

> **注意:** 入金額は100円単位で指定してください。100円の倍数でない場合は `UNSUCCESS` が返ります。

---

### Withdraw — 出金

I-PAT 口座から登録口座へ全額出金します。

```csharp
RETURN_VALUE Withdraw(ushort retryCount = 10)
```

```csharp
var result = ipat.Withdraw();
```

---

### SetAutoDepositFlag — 自動入金設定

馬券購入時に残高不足が発生した場合、自動で入金を行う機能を設定します。

```csharp
RETURN_VALUE SetAutoDepositFlag(bool enable, uint amount = 1000, ushort timeoutMs = 10000)
```

| 引数 | 説明 |
|---|---|
| `enable` | `true`: 有効 / `false`: 無効 |
| `amount` | 自動入金額（円・100円単位、デフォルト: 1000円） |
| `timeoutMs` | 入金反映確認タイムアウト（ミリ秒、デフォルト: 10000ms） |

```csharp
// 残高不足時に自動で 5,000円 入金（反映確認タイムアウト 15秒）
ipat.SetAutoDepositFlag(true, 5000, 15000);

// 無効化
ipat.SetAutoDepositFlag(false);
```

---

### GetBetInstance — 馬券購入情報の構築

買い目文字列から `ST_BET_DATA` を構築します。`Bet` を呼び出す前に必ずこの関数で購入情報を生成してください。

```csharp
RETURN_VALUE GetBetInstance(
    KAISAI place, byte raceNo,
    ushort year, byte month, byte day,
    HOUSHIKI houshiki, SHIKIBETSU shikibetsu,
    uint amount, string kaime,
    out ST_BET_DATA betData)
```

| 引数 | 説明 |
|---|---|
| `place` | 開催場（`KAISAI` 列挙値） |
| `raceNo` | レース番号（1〜12） |
| `year` / `month` / `day` | 開催日 |
| `houshiki` | 方式（`HOUSHIKI` 列挙値） |
| `shikibetsu` | 式別（`SHIKIBETSU` 列挙値） |
| `amount` | 1点あたりの購入金額（100円単位） |
| `kaime` | 買い目文字列（後述） |
| `betData` | 出力: 構築された購入情報 |

```csharp
// 東京 11R、三連単フォーメーション、1,2 → 3 → 4,5、200円
var ret = ipat.GetBetInstance(
    KAISAI.TOKYO, 11,
    2025, 5, 25,
    HOUSHIKI.FORMATION, SHIKIBETSU.TRIFECTA,
    200, "1,2-3-4,5",
    out var betData);
```

---

### Bet — 馬券購入

`GetBetInstance` で生成した `ST_BET_DATA` の配列を渡して馬券を購入します。  
異なる開催場の買い目も一括で渡せます（中央・地方・海外を自動振り分け）。

```csharp
RETURN_VALUE Bet(ST_BET_DATA[] betDataArray, ushort waitMs = 500)
```

| 引数 | 説明 |
|---|---|
| `betDataArray` | 購入情報の配列 |
| `waitMs` | 購入リクエスト間隔（ミリ秒、デフォルト: 500ms） |

```csharp
// 単一購入
ipat.Bet(new[] { betData });

// 複数まとめて購入
var bets = new ST_BET_DATA[3];
ipat.GetBetInstance(KAISAI.TOKYO,    11, 2025, 5, 25, HOUSHIKI.NORMAL,    SHIKIBETSU.WIN,      100, "1",     out bets[0]);
ipat.GetBetInstance(KAISAI.NAKAYAMA,  9, 2025, 5, 25, HOUSHIKI.BOX,       SHIKIBETSU.TRIFECTA, 100, "1,3,5", out bets[1]);
ipat.GetBetInstance(KAISAI.OI,        7, 2025, 5, 25, HOUSHIKI.NORMAL,    SHIKIBETSU.WIN,      100, "2",     out bets[2]);

ipat.Bet(bets);
```

---

### GetBetInstanceWin5 / BetWin5 — WIN5 購入

WIN5 の買い目文字列から購入情報を構築し、購入します。

```csharp
RETURN_VALUE GetBetInstanceWin5(
    uint amount, ushort year, byte month, byte day,
    string kaime, out ST_BET_DATA_WIN5 betData)

RETURN_VALUE BetWin5(ST_BET_DATA_WIN5 betData, ushort waitMs = 500)
```

```csharp
// 5レース分の買い目（各レースをハイフン区切り、複数頭はカンマ区切り）
var ret = ipat.GetBetInstanceWin5(
    100, 2025, 5, 25,
    "1,2-3-4,5-2,6-1",
    out var win5Data);

if (ret.HasFlag(RETURN_VALUE.SUCCESS))
{
    ipat.BetWin5(win5Data);
}
```

---

### GetPurchaseData — 購入履歴取得

当日・前日の馬券購入履歴、残高、購入可能件数を取得します。

```csharp
RETURN_VALUE GetPurchaseData(out ST_PURCHASE_DATA purchaseData)
```

```csharp
var ret = ipat.GetPurchaseData(out var data);
if (ret.HasFlag(RETURN_VALUE.SUCCESS))
{
    Console.WriteLine($"残高: {data.unBalance} 円");
    Console.WriteLine($"当日購入: {data.unDayPurchase} 円");
    Console.WriteLine($"当日払戻: {data.unDayPayout} 円");
    Console.WriteLine($"馬券件数: {data.unTicketCount} 件");
}
```

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

### KAISAI（開催場）

#### 中央競馬
`SAPPORO`（札幌）/ `HAKODATE`（函館）/ `FUKUSHIMA`（福島）/ `NIIGATA`（新潟）/ `TOKYO`（東京）/ `NAKAYAMA`（中山）/ `CHUKYO`（中京）/ `KYOTO`（京都）/ `HANSHIN`（阪神）/ `KOKURA`（小倉）

#### 地方競馬
`SONODA`（園田）/ `HIMEJI`（姫路）/ `NAGOYA`（名古屋）/ `MONBETSU`（門別）/ `MORIOKA`（盛岡）/ `MIZUSAWA`（水沢）/ `URAWA`（浦和）/ `FUNABASHI`（船橋）/ `OI`（大井）/ `KAWASAKI`（川崎）/ `KASAMATSU`（笠松）/ `KANAZAWA`（金沢）/ `KOCHI`（高知）/ `SAGA`（佐賀）

#### 海外競馬
`LONGCHAMP`（ロンシャン）/ `SHATIN`（シャティン）/ `SANTAANITA`（サンタアニタ）/ `DEAUVILE`（ドーヴィル）/ `CHURCHILLDOWNS`（チャーチルダウンズ）/ `ABDULAZIZ`（キングアブドゥルアジーズ）

### SHIKIBETSU（式別）

| 値 | 式別 |
|---|---|
| `WIN` | 単勝 |
| `PLACE` | 複勝 |
| `BRACKETQUINELLA` | 枠連 |
| `QUINELLAPLACE` | ワイド |
| `QUINELLA` | 馬連 |
| `EXACTA` | 馬単 |
| `TRIO` | 三連複 |
| `TRIFECTA` | 三連単 |

### HOUSHIKI（方式）

| 値 | 方式 |
|---|---|
| `NORMAL` | 通常 |
| `FORMATION` | フォーメーション |
| `BOX` | ボックス |

### RETURN_VALUE（戻り値）

ビットフラグ形式です。`HasFlag()` で判定してください。

| 値 | 意味 |
|---|---|
| `SUCCESS` | 処理に成功 |
| `UNSUCCESS` | 処理に失敗（パラメータ不正・残高不足等） |
| `FAILED_CHUOU` | 中央競馬での処理に失敗 |
| `FAILED_CHIHOU` | 地方競馬での処理に失敗 |
| `FAILED_COMMUNICATE_CHUOU` | 中央競馬との通信に失敗 |
| `FAILED_COMMUNICATE_CHIHOU` | 地方競馬との通信に失敗 |

```csharp
var ret = ipat.Bet(new[] { betData });

if (ret.HasFlag(RETURN_VALUE.SUCCESS))             Console.WriteLine("購入成功");
if (ret.HasFlag(RETURN_VALUE.FAILED_CHUOU))        Console.WriteLine("中央競馬での処理に失敗");
if (ret.HasFlag(RETURN_VALUE.FAILED_COMMUNICATE_CHUOU)) Console.WriteLine("中央競馬との通信に失敗");
```

---

## 総合的な使用例

```csharp
using IpatHelperNet;

using var ipat = new IpatHelper();

// ログイン
var loginResult = ipat.Login("1234567890", "12345678", "1234", "12345");
if (!loginResult.HasFlag(RETURN_VALUE.SUCCESS))
{
    Console.WriteLine("ログイン失敗");
    return;
}

// 残高不足時に自動で 10,000円 入金
ipat.SetAutoDepositFlag(true, 10000);

// 購入情報を複数構築
var bets = new ST_BET_DATA[2];

// 東京11R 馬連フォーメーション 1,2 - 3,4,5（200円）
ipat.GetBetInstance(
    KAISAI.TOKYO, 11, 2025, 5, 25,
    HOUSHIKI.FORMATION, SHIKIBETSU.QUINELLA,
    200, "1,2-3,4,5", out bets[0]);

// 大井7R 単勝 3番（100円）
ipat.GetBetInstance(
    KAISAI.OI, 7, 2025, 5, 25,
    HOUSHIKI.NORMAL, SHIKIBETSU.WIN,
    100, "3", out bets[1]);

// 一括購入
var betResult = ipat.Bet(bets);
if (betResult.HasFlag(RETURN_VALUE.SUCCESS))
{
    Console.WriteLine("購入成功");
}

// 購入履歴を確認
var histResult = ipat.GetPurchaseData(out var history);
if (histResult.HasFlag(RETURN_VALUE.SUCCESS))
{
    Console.WriteLine($"残高: {history.unBalance} 円");
    Console.WriteLine($"当日購入: {history.unDayPurchase} 円 / 払戻: {history.unDayPayout} 円");
}

// ログアウト（using により自動実行）
```

---

## 注意事項

- 本ライブラリは **Windows 専用**です
- 本ライブラリを使用した馬券購入は**実際の金銭を伴います**。十分にテストしてからご使用ください
- I-PAT の仕様変更により動作しなくなる場合があります
- ログイン情報はメモリ上にのみ保持され、ファイルへの保存は行いません
- 全 API はスレッドセーフです（内部でクリティカルセクションによる排他制御を実施）

---

## ライセンス

MIT License
