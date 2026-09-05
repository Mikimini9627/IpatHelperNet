# IpatHelperNet

[![NuGet](https://img.shields.io/nuget/v/IpatHelperNet.svg)](https://www.nuget.org/packages/IpatHelperNet)
[![NuGet Downloads](https://img.shields.io/nuget/dt/IpatHelperNet.svg)](https://www.nuget.org/packages/IpatHelperNet)

JRA I-PAT（インターネット投票）への馬券購入・入出金・購入履歴取得・オッズ取得・出馬表取得・お知らせ取得を自動化する Windows 用 C# ラッパーライブラリです。
内部でネイティブ DLL（`IpatHelper.dll`）を P/Invoke 経由で呼び出しており、中央競馬・地方競馬・海外競馬・WIN5 に対応しています。

> **重要:** すべての API は `IpatHelper` クラスの **静的メソッド**として提供され、戻り値は **`uint`（ビットフラグ）** です。
> インスタンス化や `using`（`IDisposable`）は不要・非対応です。判定は `RETURN_VALUE` 定数との AND 演算で行います。

---

## 目次

- [動作環境](#動作環境) / [インストール](#インストール) / [クイックスタート](#クイックスタート)
- **API リファレンス**
  - 認証: [Login](#login--ログイン) / [Logout](#logout--ログアウト)
  - 入出金: [Deposit](#deposit--入金) / [Withdraw](#withdraw--出金) / [SetAutoDepositFlag](#setautodepositflag--自動入金設定) / [失敗したときの調べ方](#入出金が失敗したときの調べ方)
  - 購入: [GetBetInstance](#getbetinstance--馬券購入情報の構築) / [Bet](#bet--馬券購入) / [GetBetInstanceWin5 / BetWin5](#getbetinstancewin5--betwin5--win5-購入) / [BetWin5Auto](#betwin5auto--win5-のセレクト--ランダム購入)
  - 情報取得: [GetPurchaseData](#getpurchasedata--購入履歴取得) / [GetOdds](#getodds--オッズ取得) / [GetRaceCard](#getracecard--出馬表取得) / [GetKaisaiList](#getkaisailist--開催場一覧取得) / [GetNotice](#getnotice--お知らせ取得)
  - ログ: [SetLogCallback](#setlogcallback--ログの取得)
- [買い目文字列の書式](#買い目文字列の書式)
- [購入明細の読み方](#購入明細の読み方) — **`horseNo[]` を読む前に必ず参照してください**
- [列挙型](#列挙型) / [定数](#定数)
- [総合的な使用例](#総合的な使用例) / [トラブルシューティング](#トラブルシューティング)
- [DLL を明示的にアンロードする場合](#dll-を明示的にアンロードする場合)
- [開発者向け: リリース手順](#開発者向け-リリース手順) / [注意事項](#注意事項)

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
<PackageReference Include="IpatHelperNet" Version="1.1.32" />
```

> 最新のバージョン番号は、ページ冒頭の NuGet バッジ、または [NuGet ギャラリー](https://www.nuget.org/packages/IpatHelperNet) で確認できます。

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

// 受付時間外・メンテナンス中はここが立つ。即時リトライは必ず失敗する
if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_OUT_OF_SERVICE) != 0)
{
    Console.WriteLine("サービス時間外です。時間をおいて再試行してください");
}
```

> 中央・地方のどちらか一方でも成功すれば `SUCCESS` が立ちます。失敗した系統のみ購入できません。

> **`FAILED_OUT_OF_SERVICE`（サービス時間外）** は `Login` でのみ立ち、`FAILED_CHUOU` / `FAILED_CHIHOU` と**併せて**立ちます。
> 最も多い原因は投票受付時間外で、**特に地方競馬は営業時間外に必ずこの状態になります**（不具合ではありません）。
> メンテナンス中も同じ状態になるため両者は区別できません。
> **このフラグが立った場合、即座のリトライは必ず失敗します。** ID・パスワード誤りや一時的な通信障害とは扱いを分け、時間をおいて再試行してください。

---

### Logout — ログアウト

I-PAT からログアウトし、内部セッション情報・自動入金設定を初期化します。
サーバへの通知に失敗しても後始末は必ず行われ、`Logout` 自体は成功を返します。

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
static uint Deposit(uint depositValue, ushort retryCount = DEFAULT_RETRY_COUNT)
```

| 引数 | 説明 |
|---|---|
| `depositValue` | 入金額（円・100円単位） |
| `retryCount` | リトライ回数（デフォルト: 10。適用範囲は下記） |

```csharp
uint ret = IpatHelper.Deposit(10000);  // 10,000円 入金
```

- 入金額は **100円以上かつ100円単位**で指定してください。条件を満たさない場合は `UNSUCCESS` が返ります（上限はライブラリ側では設けていません。実際の上限は登録口座・金融機関側の条件に従います）。
- 入金指示の完了後、**入金額が残高へ加算されたことを確認できるまで待機**し、反映を確認できた場合のみ成功を返します。待機時間の上限は `SetAutoDepositFlag` の `confirmTimeout`（既定 10,000ms）です。
- **`retryCount` が適用されるのは入金実行前の準備段階だけです。** 入金実行そのものは、応答を受信できなかった場合でもサーバ側で成立している可能性があるため**再送しません**（二重入金の防止）。この場合は残高への反映で成否を判定します。
- **即PAT（ネットバンク）会員専用です。** A-PAT 会員は入金 URL が存在しないため `UNSUCCESS` を返します。
- **登録口座が PayPay（コード決済アプリ）の場合は利用できません。** 通信を行わず `UNSUCCESS` を返します。入金は PayPay アプリ側で操作してください。
  > **PayPay*銀行*は従来どおり利用できます。** 拒否されるのは PayPay*アプリ*を登録口座にしている会員だけです（両者は別物です）。

---

### Withdraw — 出金

I-PAT 口座から登録口座へ**全額**出金します。出金額の指定は不要です。

```csharp
static uint Withdraw(ushort retryCount = DEFAULT_RETRY_COUNT)
```

```csharp
uint ret = IpatHelper.Withdraw();
```

- 出金指示の完了後、**残高が 0 になったことを確認できるまで待機**し、反映を確認できた場合のみ成功を返します。
- `retryCount` の適用範囲は `Deposit` と同じで、**出金の実行そのものは再送しません**（二重出金の防止）。
- `Deposit` と同じく**即PAT（ネットバンク）会員専用**で、**登録口座が PayPay（コード決済アプリ）の場合は `UNSUCCESS`** を返します。

---

### 入出金が失敗したときの調べ方

入出金は投票系と違い、機械可読なエラーコードを返しません。**失敗の原因を知るには [`SetLogCallback`](#setlogcallback--ログの取得) が必須です。**

`LogLevel.Error` を指定すると、次の情報が通知されます。

- **どの段階で失敗したか** — 入金ページの取得 / 確認 / 実行 / 実行後の完了画面確認
- **応答を受信できたかどうか** — 受信できていない場合、サーバ側で成立している可能性があります
- **着地した画面の ID とタイトル**
- **残高反映待機の実測値** — 経過時間・確認回数・タイムアウト設定値。タイムアウトした場合は「反映が遅いだけ」の可能性があるため、`SetAutoDepositFlag` の `confirmTimeout` を延ばして解消するか確認できます

さらに `LogLevel.Trace` を指定すると、サーバ側の拒否理由（時間外・パスワード誤り・メンテナンス等）を含む**応答本文の抜粋**も通知されます。

> ⚠️ 応答本文の抜粋には**口座番号や残高が含まれることがあります。** そのため既定（`LogLevel.Info`）では出力されません。調査時のみ `Trace` を指定し、ログの取り扱いにご注意ください。

---

### SetAutoDepositFlag — 自動入金設定

馬券購入時に残高不足が発生した場合、自動で入金を行う機能を設定します。

```csharp
static uint SetAutoDepositFlag(bool enable, uint depositValue = DEPOSIT_DEFAULT_VALUE, ushort confirmTimeout = DEFAULT_CONFIRM_TIMEOUT)
```

| 引数 | 説明 |
|---|---|
| `enable` | `true`: 有効 / `false`: 無効 |
| `depositValue` | 自動入金額（円・100円単位、デフォルト: 1000円）。`enable` が `false` の場合は検証しません |
| `confirmTimeout` | 残高反映の確認タイムアウト（ミリ秒、デフォルト: 10000ms）。**`Deposit` / `Withdraw` の反映待機にも使われます** |

```csharp
// 残高不足時に自動で 5,000円 入金（反映確認タイムアウト 15秒）
IpatHelper.SetAutoDepositFlag(true, 5000, 15000);

// 無効化
IpatHelper.SetAutoDepositFlag(false);
```

- 入金後、残高への反映を最大 `confirmTimeout` ミリ秒待機します。タイムアウトした場合は購入を中止します。
- 入金しても残高が購入金額に満たない場合は、入金を行わず `UNSUCCESS` を返します。
- 自動入金は `Deposit` と同じ経路を使うため、**即PAT（ネットバンク）会員専用**という制約もそのまま当てはまります。

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
| `place` | 開催場（`Kaisai` 列挙値）。未定義の値は `UNSUCCESS` |
| `raceNo` | レース番号（**1〜14**）。範囲外は `UNSUCCESS` |
| `kaisaibi` | 開催日（`DateTime`） |
| `houshiki` | 方式（`Houshiki` 列挙値） |
| `shikibetsu` | 式別（`Shikibetsu` 列挙値） |
| `kingaku` | 1点あたりの購入金額（100円以上 `MAX_TOTAL_AMOUNT_PER_SEND` 円以下、100円単位） |
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

Console.WriteLine($"合計 {betData.totalAmount} 円");   // 4点 × 200円 = 800円
```

- 合計購入金額は `betData.totalAmount` に自動計算されて格納されます。
- **1回の送信あたりの合計購入金額は 1,000,000円（`MAX_TOTAL_AMOUNT_PER_SEND`）が上限**です。1点でもこの上限が効くため、1点あたりの金額の上限もこの値になります。
- 馬番は **1〜18**（海外開催は **1〜24**）で指定してください。**範囲外の馬番が含まれる場合は、その馬番を無視するのではなく `UNSUCCESS` を返します**（指定より少ない点数で購入されるのを防ぐため）。
- **海外開催では枠連（`BRACKETQUINELLA`）を購入できません**（枠の概念が無いため）。指定すると `UNSUCCESS` になります。
- マルチ（`WHEEL_MULTI_*`）を指定すると、`betData.houshiki` は基底のながし方式に正規化され、`betData.multi` が `1` になります。
- 本 API は**通信を行わないため他の API の実行中でも並行して呼び出せます**。

---

### Bet — 馬券購入

`GetBetInstance` で生成した `ST_BET_DATA` のリストを渡して馬券を購入します。
異なる開催場の買い目も一括で渡せます（中央・地方・海外を自動振り分け）。

```csharp
static uint Bet(List<ST_BET_DATA> betDataList, ushort waitMiliSeconds = DEFAULT_BET_INTERVAL_MANAGED)
```

| 引数 | 説明 |
|---|---|
| `betDataList` | 購入情報のリスト |
| `waitMiliSeconds` | **分割送信の間隔**（ミリ秒、デフォルト: 1000ms）。**タイムアウトではありません** |

- 購入件数が1回の送信上限（中央: 255件、地方: 50件）を超える場合、**自動的に分割して送信**します。`waitMiliSeconds` はそのときのリクエスト間隔です。間隔が短いと購入に失敗することがあるため、ネットワーク環境に応じて調整してください。
- ネイティブ DLL 側の既定値は 500ms（`DEFAULT_BET_INTERVAL`）ですが、**本ラッパーはより余裕を持たせた 1000ms（`DEFAULT_BET_INTERVAL_MANAGED`）を既定で渡します。** DLL と同じ値にしたい場合は `IpatHelper.DEFAULT_BET_INTERVAL` を明示的に渡してください。
- 購入前に残高と購入可能件数を確認します。自動入金が有効な場合は残高不足時に自動入金します。
- 手で組み立てた `ST_BET_DATA` も受け取れるよう、レース番号（1〜14）と曜日（1〜7）を送信前に再検証します。範囲外の値は `UNSUCCESS` になります。
- 応援馬券（`WINPLACE`）は送信時に**単勝と複勝の2点へ展開**されます。件数・金額の上限は展開後の値で判定されます。

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
static uint BetWin5(ST_BET_DATA_WIN5 betData, ushort waitMiliSeconds = DEFAULT_BET_INTERVAL_MANAGED)
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

- 金額の上限は `GetBetInstance` と同じ **1,000,000円**です。馬番は 1〜18 で指定してください。
- 1回の購入上限（50組み合わせ）を超える場合は自動的に分割送信します。`waitMiliSeconds` は `Bet` と同じく**分割送信の間隔**です。
- `GetBetInstanceWin5` も `GetBetInstance` と同じく通信を行いません。

---

### BetWin5Auto — WIN5 のセレクト / ランダム購入

WIN5 を「セレクト」または「ランダム」で購入します。買い目を指定する `BetWin5` と違い、**買い目はサーバが生成**します。

```csharp
static uint BetWin5Auto(Win5AutoMode mode, string axisUmaban, uint betCount, uint kingaku, DateTime kaisaibi)
```

| `Win5AutoMode` | 動作 |
|---|---|
| `Select` | `axisUmaban` で軸馬を指定し、`0` にしたレースはサーバが選ぶ（**`0` にできるのは 1〜4 レース**） |
| `Random` | 5 レースすべてサーバが選ぶ（`axisUmaban` は `null` で可） |

```csharp
// ランダムで 10 点 × 100円
uint ret = IpatHelper.BetWin5Auto(IpatHelper.Win5AutoMode.Random, null, 10, 100, new DateTime(2026, 8, 9));

// セレクト: 1R の軸だけ決めて 20 点 × 100円
ret = IpatHelper.BetWin5Auto(IpatHelper.Win5AutoMode.Select, "6,0,0,0,0", 20, 100, new DateTime(2026, 8, 9));
```

- **生成された買い目はそのまま購入されます。** 内容を事前に確認する手段はないため、呼び出す前に必ず利用者の確認を取ってください。
- **セレクトは「一部のレースだけ軸を決めて、残りをサーバに選ばせる」方式です。** `0`（おまかせ）にするレース数は **1〜4** でなければならず、次の 2 つは送信せずに `UNSUCCESS` を返します。

  | 指定 | 例 | 理由 |
  |---|---|---|
  | 全レース `0` | `"0,0,0,0,0"` | ランダムと同じになりセレクトの意味がありません。`Random` を使ってください |
  | `0` が 1 つも無い | `"3,7,1,5,2"` | 買い目が 1 通りに決まり、依頼した点数を生成できません。`BetWin5` で直接指定してください |

- 点数の上限は 50 点（`MAX_WIN5_AUTO_BET_COUNT`）です。分割送信は行いません。
- 合計金額は生成された買い目から算出され、1,000,000 円（`MAX_TOTAL_AMOUNT_PER_SEND`）を超える場合は送信せずに `UNSUCCESS` を返します。
- WIN5 は中央競馬のみ対応です。

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

    // 履歴の欠けを検出したい場合は、SUCCESS と同時に立つ失敗フラグも見る
    if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_CHUOU) != 0)  Console.WriteLine("※中央の履歴が欠けています");
    if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_CHIHOU) != 0) Console.WriteLine("※地方の履歴が欠けています");
}
```

**中央と地方の両方に投票した場合**

購入履歴は会場ごとに別々に保持されているため、**ログイン済みの会場すべてから取得して連結**します。中央と地方の両方へ投票した購入も、1回の `GetPurchaseData` で両方の馬券が得られます。

| 項目 | 扱い |
|---|---|
| 馬券（`ticketData`） | 中央 → 地方の順に連結（各会場内は前日 → 当日） |
| 残高・購入可能件数・当日/累計の金額 | **合算しません。** 中央・地方は同じ即PAT 口座を共有するため、どちらか一方の値をそのまま返します |
| 海外の馬券 | 中央の履歴に含まれます（明細の `betFlag` が `BET_FLAG.INTERNATIONAL`） |

- **片方の会場だけ取得に失敗した場合は、取得できた分を返したうえで** `FAILED_CHUOU` / `FAILED_CHIHOU` を立てます。この場合 `SUCCESS` と**同時に**立つため、履歴の欠けを検出したいときはこれらのフラグも確認してください。両方失敗した場合のみ `SUCCESS` は立ちません。
- 受付時刻を読み取れなかった受付は、その受付を捨てるのではなく `hour` / `minute` が `0` のまま返されます。金額・明細は正常です。

> 明細（`ST_TICKET_DATA_DETAIL`）から買い目を復元する方法は「[購入明細の読み方](#購入明細の読み方)」を参照してください。
> **列を機械的に `-` で連結すると誤った買い目になります。**

---

### GetOdds — オッズ取得

指定レース・式別のオッズを取得します（**中央競馬・地方競馬・海外競馬**に対応）。単勝・複勝は基本オッズ、枠連〜三連単は全通りのオッズ表を取得します。

```csharp
static uint GetOdds(Kaisai place, byte raceNo, Shikibetsu shikibetsu, out ST_ODDS_DATA oddsData)
```

- ネイティブ側で確保されたメモリはラッパー内部で解放するため、呼び出し側での解放は不要です。
- オッズは 10 倍の整数（`odds`）で格納されます（例: 12.3 倍 → `123`）。実際の倍率は `odds / 10.0`。
- 複勝・ワイドは下限を `odds`、上限を `oddsHigh` に格納します。
- `status` が `1`（発売中止）／`2`（オッズ未取得）の場合、`odds` / `oddsHigh` は `0` です。
- **海外開催は中央競馬へのログインが必要**です。海外競馬に枠は無いため、`BRACKETQUINELLA`（枠連）を指定すると `UNSUCCESS` を返します。それ以外の式別で取得できる内容は中央・地方と同じです。
- **応援馬券（`WINPLACE`）はオッズの式別ではないため指定できません**（`UNSUCCESS`）。単勝・複勝を個別に取得してください。
- 指定した開催場がその日開催されていない場合は `UNSUCCESS` を返します。

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

指定レースの出馬表（出走馬一覧）を取得します（**中央競馬・地方競馬・海外競馬**に対応）。各出走馬の枠番・馬番・馬名・性齢・馬体重・騎手・斤量・調教師・単勝人気・単勝/複勝オッズを取得します。

```csharp
static uint GetRaceCard(Kaisai place, byte raceNo, out ST_RACECARD_DATA raceCard)
```

- ネイティブ側で確保されたメモリはラッパー内部で解放するため、呼び出し側での解放は不要です。
- 文字列（馬名・騎手名・調教師名など）は UTF-8 からデコード済みの `string` として格納されます。
- `raceName` は**レース名**です（取得できない場合は空文字）。
- `deadline` は**発売締切時刻**（`"HH:MM"`）、`raceStatus` はそのレースの**発売状態**です。**追加の通信は発生しません。** 締切時刻だけでは購入可否が判断できないため併せて参照してください。
- 斤量・オッズは 10 倍の整数で格納されます。実際の値は `/ 10.0`。
- 指定した開催場がその日開催されていない場合は `UNSUCCESS` を返します。
- **海外開催は中央競馬へのログインが必要**です。また **I-PAT が返す項目が国内より少なく**、取得できるのは `umaban` / `horseName` / `winPopular` / 単勝・複勝オッズ と `raceName` / `deadline` / `raceStatus` だけです。**`wakuban` / `sex` / `age` / `weight` / `jockeyName` / `burden` / `trainerName` は 0 または空文字**になります（海外競馬に枠・馬体重・斤量などの概念が無いため）。

```csharp
uint ret = IpatHelper.GetRaceCard(IpatHelper.Kaisai.TOKYO, 11, out var raceCard);
if ((ret & 1) == 1)
{
    Console.WriteLine($"レース名: {raceCard.raceName}");
    Console.WriteLine($"締切: {raceCard.deadline} / 発売状態: {raceCard.raceStatus}");
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
| | `raceName` | レース名（取得不可時は空文字） |
| | `deadline` | 発売締切時刻 "HH:MM"（取得不可時は空文字） |
| | `raceStatus` | 発売状態 `RACE_STATUS`（`ON_SALE`=0 / `CLOSED`=1 / `CANCELED`=2 / `BEFORE_SALE`=3 / `UNKNOWN`=0xFF） |
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

### GetKaisaiList — 開催場一覧取得

本日開催されている開催場の一覧を取得します。開催場ごとに、レース番号・発売締切時刻・発売状態・レース名も併せて返します。

```csharp
static uint GetKaisaiList(out ST_KAISAI_DATA kaisaiData)
```

- **中央競馬・地方競馬・海外競馬に対応**しています。ログイン済みの系統を対象とし、海外は中央にログインしていれば含まれます。
- **系統ごとに 1 回ずつ、最大 3 回の通信で全開催場が得られます。**
  「どの開催場が開催中か」を調べるために `GetRaceCard` を開催場の数だけ呼ぶ必要はありません。
- 片方の系統だけ失敗した場合は、**取得できた分を返したうえで** `FAILED_CHUOU` / `FAILED_CHIHOU` を立てます（`SUCCESS` と同時に立ちます）。
- 開催が 1 つも無い場合は `kaisaiCount` が 0 で成功します。
- ネイティブ側で確保されたメモリはラッパー内部で解放するため、呼び出し側での解放は不要です。

```csharp
uint ret = IpatHelper.GetKaisaiList(out var kaisai);
if ((ret & 1) == 1)
{
    Console.WriteLine($"本日の開催: {kaisai.kaisaiCount} 場");
    foreach (var k in kaisai.kaisai)
    {
        Console.WriteLine($"{k.place} ({k.raceCount}R)");
        foreach (var r in k.races)
        {
            Console.WriteLine($"  {r.raceNo,2}R {r.deadline,-5} {r.raceStatus,-11} {r.raceName}");
        }
    }
}
```

`ST_KAISAI_DATA` / `ST_KAISAI_ITEM` / `ST_KAISAI_RACE` の各フィールド:

| 構造体 | フィールド | 内容 |
|---|---|---|
| `ST_KAISAI_DATA` | `kaisaiCount` / `kaisai` | 開催場数 / 開催場一覧 |
| `ST_KAISAI_ITEM` | `place` | 開催場（`Kaisai`） |
| | `raceCount` / `races` | レース数 / レース一覧 |
| `ST_KAISAI_RACE` | `raceNo` | レース番号（1 始まり） |
| | `raceStatus` | 発売状態（`RACE_STATUS`） |
| | `deadline` | 発売締切時刻 "HH:MM"（取得不可時は空文字） |
| | `raceName` | レース名（取得不可時は空文字。**海外開催でも取得できます**） |

> **レース番号は `raceNo` で判断してください。** `races` はレース番号順に並びますが、欠番があり得るため「添字 + 1」と一致するとは限りません。

> 締切時刻だけでは購入可否が判断できないため、`raceStatus` も併せて参照してください。

---

### GetNotice — お知らせ取得

現在有効なお知らせを取得します（**中央競馬・地方競馬**に対応）。強制表示お知らせ本文に加え、お知らせ一覧（タイトル・日付・URL 等）を全件取得します。

```csharp
static uint GetNotice(out ST_NOTICE_DATA notice)
```

- ネイティブ側で確保されたメモリはラッパー内部で解放するため、呼び出し側での解放は不要です。
- 文字列は UTF-8 からデコード済みの `string` として格納されます。
- お知らせが無い場合は `message` が空文字・`itemCount` が 0 で成功します。

```csharp
uint ret = IpatHelper.GetNotice(out var notice);
if ((ret & 1) == 1)
{
    if (!string.IsNullOrEmpty(notice.message))
    {
        Console.WriteLine($"強制表示: {notice.message}");
    }
    Console.WriteLine($"お知らせ {notice.itemCount} 件");
    foreach (var item in notice.items)
    {
        Console.WriteLine($"[{item.date}] {item.title}  {item.url}");
    }
}
```

`ST_NOTICE_DATA` / `ST_NOTICE_ITEM` の各フィールド:

| 構造体 | フィールド | 内容 |
|---|---|---|
| `ST_NOTICE_DATA` | `message` | 強制表示お知らせ本文（無い場合は空文字） |
| | `noticeNo` / `noticeType` | お知らせ番号 / 種別 |
| | `itemCount` / `items` | お知らせ一覧の件数 / 配列 |
| `ST_NOTICE_ITEM` | `title` / `date` | タイトル / 日付テキスト |
| | `url` / `icon` / `color` | リンクURL / アイコン / 日付表示色 |

---

### SetLogCallback — ログの取得

DLL 内部のログを受け取るハンドラを登録します（`null` で解除）。**Release ビルドの DLL でも取得できます。**

```csharp
static void SetLogCallback(LogHandler handler, LogLevel minLevel = LogLevel.Info)
```

**入出金の失敗調査にはこの API が必須です。** 入出金はサーバレンダリングの HTML フォームで、`erc` / `erm` のような機械可読なエラーコードを返しません。失敗した段階・画面 ID・画面タイトルは `LogLevel.Error` で通知されますが、**サーバ側の拒否理由が載る応答本文の抜粋は `LogLevel.Trace` を指定したときのみ**通知されます。

```csharp
IpatHelper.SetLogCallback((level, message) =>
{
    Console.WriteLine($"[{level}] {message}");
}, IpatHelper.LogLevel.Info);   // 調査時は LogLevel.Trace

// ... 各 API を実行 ...

IpatHelper.SetLogCallback(null); // 解除してからハンドラの対象を破棄する
```

| `LogLevel` | 内容 |
|---|---|
| `Trace` | 詳細トレース。**入出金失敗時の応答本文の抜粋はこのレベルのみ** |
| `Info` | 情報（既定） |
| `Warn` | 警告 |
| `Error` | エラー。失敗した段階・画面 ID・タイトルはこのレベル |

- ハンドラは **DLL 内部ロックを保持したまま**呼ばれます。**ハンドラ内から本クラスの API を呼び返さないでください**（デッドロックします）。
- `Login` 実行中は中央・地方の **2 スレッドから同時に**呼ばれます。
- `null` を渡して戻った時点で実行中のハンドラは存在しないため、ハンドラの対象を安全に破棄できます。
- メッセージは UTF-8 からデコード済みの `string` です。
- 応答本文の抜粋には**口座番号や残高が含まれ得ます**。`Trace` は調査時のみ指定し、ログの取り扱いに注意してください。

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
| ながし(`WHEEL_1ST`) | 三連単 | `"1-2,3,4"` | 1着軸1番 / 相手2,3,4番 |
| ながし(`WHEEL_1ST_2ND`) | 三連単 | `"1-2-3,4"` | 1着軸1 / 2着軸2 / 相手3,4(着順) |
| マルチ(`WHEEL_MULTI_AXIS1`) | 三連単 | `"1-2,3,4"` | 軸1と相手2,3,4の全着順(18点) |
| マルチ(`WHEEL_MULTI_AXIS2`) | 三連単 | `"1-2-3,4"` | 軸2頭(1,2)と相手3,4の全着順 |

ながし・マルチは中央・地方・海外すべての開催場で指定できます（マルチは馬単・三連単のみ）。

### WIN5 の例

| 買い目文字列 | 説明 |
|---|---|
| `"1-2-3-4-5"` | 各レース1頭ずつ指定 |
| `"1,2-3-4,5-2,6-1"` | 一部のレースで複数頭指定 |

### 枠連の指定

- 買い目は馬番ではなく**枠番（1〜8）**で指定します。9 以上を指定すると `UNSUCCESS` で失敗します。
- **ゾロ目（同枠の2頭による決着）は同じ枠を2つ指定します**（`"3-3"`）。通常方式で枠を1つだけ指定した `"3"` も同じ意味です。
- **ゾロ目が成立する枠かどうかは検証しません。** その枠に2頭以上いるかは出馬表を見ないと分からないためです。同枠のない枠でゾロ目を指定すると、購入時にサーバ側で失敗します。
- 海外開催では購入できません（`UNSUCCESS`）。

### 応援馬券の指定

- 方式は**通常（`NORMAL`）のみ**、買い目は**1頭のみ**（`"7"` のように馬番ひとつ）です。ボックス・フォーメーション・ながしは指定できません。
- **合計購入金額は指定金額の2倍**になります（100円指定 = 単勝100円 + 複勝100円）。点数も2点として数えます。

---

## 購入明細の読み方

`GetPurchaseData` が返す `ST_TICKET_DATA_DETAIL` は、**購入時の指定そのものではなく、投票内容から復元した値**です。
**`horseNo[]` の各列が何を指すかは `method`（方式）と `type`（式別）の組み合わせで変わります。**
列を機械的に `-` で連結すると誤った買い目になります。

### horseNo[] のビット表現

- **bit 0 が馬番 1**、bit 1 が馬番 2 … と続きます。判定は `(horseNo[列] & (1u << (馬番 - 1))) != 0` です。
- **枠連（`BRACKETQUINELLA`）だけは馬番ではなく枠番**で、bit 0 が枠 1、bit 7 が枠 8 です。
- 有効なビットの範囲は **国内 18 頭 / 海外 24 頭 / 枠連 8 枠**。範囲外のビットは立ちません。
- **使わない列は 0** です。`0` は「指定なし」ではなく「その方式・式別には存在しない列」を意味します。列がいくつあるかは必ず下表で判断してください。
- **`horseNo[3]` と `horseNo[4]` は WIN5 専用**です。WIN5 以外の明細では常に 0 で、使うのは `horseNo[0..2]` だけです。

```csharp
// 1つの列から馬番の一覧を取り出す
static IEnumerable<int> Umaban(uint mask, int max = 18)
{
    for (int n = 1; n <= max; n++)
        if ((mask & (1u << (n - 1))) != 0) yield return n;
}
```

### method / type とマルチ

`method` は `Houshiki`、`type` は `Shikibetsu` と同じ数値です。

- **マルチの判定は必ず `multi` で行ってください。`method` で判定してはいけません。**
  マルチは基底のながし方式（軸1頭 = 3 / 軸2頭 = 6）で記録されることがあるため、購入明細では
  **`method` が 3 / 6 のまま `multi = 1`** という形と、**`method` が 9 / 10** という形の**両方があり得ます**。どちらも同じ買い目です。
- **応援馬券（`WINPLACE` = 9）は `type` に現れません。** 単勝＋複勝の2点として購入されるため、履歴には `type` が 1（単勝）と 2（複勝）の別々の馬券として並びます。

### horseNo[] の列の意味（方式 × 式別）

`—` は使用しない列（常に 0）です。

**通常（`method` = 0）**

| 式別 | `horseNo[0]` | `horseNo[1]` | `horseNo[2]` |
|---|---|---|---|
| 単勝(1) / 複勝(2) | 馬 1 頭 | — | — |
| 枠連(3) | 枠 2 つ（**ゾロ目は 1 つだけ**） | — | — |
| 馬連(4) / ワイド(5) | 馬 2 頭（順不同なので同じ列） | — | — |
| 馬単(6) | 1 着馬 | 2 着馬 | — |
| 三連複(7) | 馬 3 頭（順不同なので同じ列） | — | — |
| 三連単(8) | 1 着馬 | 2 着馬 | 3 着馬 |

**フォーメーション（`method` = 1）**

| 式別 | `horseNo[0]` | `horseNo[1]` | `horseNo[2]` |
|---|---|---|---|
| 枠連(3) | 1 枠目群 | 2 枠目群 | **同枠（ゾロ目）群** ← 枠の選択ではない |
| 馬連(4) / ワイド(5) / 馬単(6) | 1 列目 | 2 列目 | — |
| 三連複(7) / 三連単(8) | 1 列目 | 2 列目 | 3 列目 |

**ボックス（`method` = 2）**

| 式別 | `horseNo[0]` | `horseNo[1]` | `horseNo[2]` |
|---|---|---|---|
| 枠連(3) 〜 三連単(8) | 選択した馬（枠）すべて | — | — |

**軸1頭系のながし（`method` = 3 / 4 / 5 / 9）** — 列は「軸」と「相手」です。

| `method` | 式別 | `horseNo[0]` | `horseNo[1]` | `horseNo[2]` |
|---|---|---|---|---|
| 3 | 枠連(3) / 馬連(4) / ワイド(5) | 軸 | 相手 | — |
| 3 | 馬単(6) / 三連単(8) | 軸（1 着） | 相手 | — |
| 3 | 三連複(7) | 軸 1 頭 | 相手 | — |
| 4 | 馬単(6) / 三連単(8) | 軸（2 着） | 相手 | — |
| 5 | 三連単(8) | 軸（3 着） | 相手 | — |
| 9 | 馬単(6) / 三連単(8) | 軸 | 相手 | — |

**軸2頭系のながし（`method` = 6 / 7 / 8 / 10）**

三連複（式別 7・`method` = 6）は順不同のため着順の意味を持たず、列は「軸」と「相手」です。

| `method` | 式別 | `horseNo[0]` | `horseNo[1]` | `horseNo[2]` |
|---|---|---|---|---|
| 6 | 三連複(7) | 軸 2 頭（同じ列にまとめて入る） | 相手 | — |

三連単（式別 8）は**列がそのまま着順**（0 = 1 着 / 1 = 2 着 / 2 = 3 着）になります。

| `method` | `horseNo[0]`（1 着） | `horseNo[1]`（2 着） | `horseNo[2]`（3 着） |
|---|---|---|---|
| 6（1・2着ながし） | 軸 | 軸 | 相手 |
| 7（1・3着ながし） | 軸 | 相手 | 軸 |
| 8（2・3着ながし） | 相手 | 軸 | 軸 |
| 10（軸2頭ながしマルチ） | 軸 | 軸 | 相手 |

### 枠連の表示に関する注意

枠連は**列の数が他の式別と違う**ため、三連単などと同じ描画処理を通すと誤った表示になります。

- **通常方式**: 買い目は `horseNo[0]` の枠だけで表せます。**ゾロ目のときは枠が 1 つしか立ちません。** 1 つしか立っていない場合は同じ枠を 2 回並べて `8-8` と表示してください。
- **フォーメーション**: 表示に使うのは `horseNo[0]`（1 枠目群）と `horseNo[1]`（2 枠目群）**だけ**です。`horseNo[2]` は「1 枠目群と 2 枠目群の両方に現れた枠 ＝ その枠をゾロ目としても買っている」ことを示す印で、**3 つ目の枠の選択ではありません。**

| 買い目 | `horseNo[0]` | `horseNo[1]` | `horseNo[2]` | 点数 | 正しい表示 |
|---|---|---|---|---|---|
| 1,2,3 → 4,5 | `1,2,3` | `4,5` | （なし） | 6 | `1,2,3 - 4,5` |
| 1,2,3 → 2,3,4 | `1,2,3` | `2,3,4` | `2,3` | 8 | `1,2,3 - 2,3,4`（うち 2-2, 3-3 はゾロ目） |
| 8 → 8 | `8` | `8` | `8` | 1 | `8-8`（ゾロ目 1 点） |

**3 行目を `8-8-8` と表示するのは誤りです。**

### WIN5 の明細

`betFlag` が `BET_FLAG.WIN5` の明細では、**`horseNo[0]`〜`horseNo[4]` が第 1〜第 5 レースの馬番**に対応します。
レース単位の情報を持たないため、次のフィールドには**意味のある値が入りません**。

| フィールド | WIN5 のときの値 |
|---|---|
| `kaisai` / `raceNo` / `week` / `method` / `type` | すべて `0xFF`（255） |

WIN5 の判定は `betFlag` で行うのが確実です。

### 解析できなかった明細

**1 件の明細を解析できなかった場合**、その明細は `decisionFlag` が **`DECISIONFLAG.PARSE_FAILED`（0）** になります。
`DECISIONFLAG` は 1 始まりのため、この値が正常な確定フラグと衝突することはありません。

| フィールド | 解析できなかった明細の値 |
|---|---|
| `decisionFlag` | `DECISIONFLAG.PARSE_FAILED`（0） |
| `betFlag` | その受付の券種（どの種別の明細が壊れたか分かるように残ります） |
| `kaisai` / `raceNo` / `week` / `method` / `type` / `horseNo[]` / `multi` | すべて 0 |

- **他の明細・他の受付は正常に返されます。** 1 件の破損で購入履歴全体を失わないための仕様です。
- **`method` / `type` に `0xFF` は入りません**（WIN5 の判定と衝突させないため）。
- **1 件も解析できなかった場合は `GetPurchaseData` 自体が失敗します。** 空の履歴として黙って返さないためです。
- 金額は明細ではなく `ST_TICKET_DATA` の `kingaku` / `payout` に入っているため、**金額の集計はこの状況でも正しく行えます。**

### 海外開催の明細

`betFlag` が `BET_FLAG.INTERNATIONAL` の明細も、列の意味は上表と**完全に同じ**です。違いは次の 2 点だけです。

- 馬番の上限が **24**（国内は 18）。
- **枠連が存在しません**（`type` に 3 は現れません）。

### 買い目を復元できない場合

方式と式別の組み合わせが I-PAT に存在しないものだった場合、`horseNo[]` は **5 列すべて 0** になります
（`method` / `type` / `multi` にはそのままの値が入ります）。正常な購入履歴では発生しませんが、
サーバの仕様変更で未知の方式が返った場合にこの形になります。
**列がすべて 0 の明細は「買い目なし」として扱ってください。** 金額の集計には影響しません。

---

## 列挙型

### Kaisai（開催場）

#### 中央競馬
`SAPPORO`（札幌）/ `HAKODATE`（函館）/ `FUKUSHIMA`（福島）/ `NIIGATA`（新潟）/ `TOKYO`（東京）/ `NAKAYAMA`（中山）/ `CHUKYO`（中京）/ `KYOTO`（京都）/ `HANSHIN`（阪神）/ `KOKURA`（小倉）

#### 地方競馬
`SONODA`（園田）/ `HIMEJI`（姫路）/ `NAGOYA`（名古屋）/ `MONBETSU`（門別）/ `MORIOKA`（盛岡）/ `MIZUSAWA`（水沢）/ `URAWA`（浦和）/ `FUNABASHI`（船橋）/ `OI`（大井）/ `KAWASAKI`（川崎）/ `KASAMATSU`（笠松）/ `KANAZAWA`（金沢）/ `KOCHI`（高知）/ `SAGA`（佐賀）

#### 海外競馬
`LONGCHAMP`（ロンシャン）/ `SHATIN`（シャティン）/ `SANTAANITA`（サンタアニタ）/ `DEAUVILLE`（ドーヴィル）/ `CHURCHILLDOWNS`（チャーチルダウンズ）/ `ABDULAZIZ`（キングアブドゥルアジーズ）/ `ASCOT`（アスコット）

> **`DEAUVILE`（`L` が1つ）は綴りを誤った旧名です。** `DEAUVILLE` と同じ値の別名として残してありますが、`[Obsolete]` が付いています。新しいコードでは `DEAUVILLE` を使ってください。

> 列挙値は固定されており、開催場が追加される場合は必ず末尾へ追加されます。既存の値がずれることはありません。

### Shikibetsu（式別）

| 値 | 式別 | 説明 |
|---|---|---|
| `WIN` | 単勝 | 1着馬を当てる |
| `PLACE` | 複勝 | 3着以内に入る馬を当てる |
| `BRACKETQUINELLA` | 枠連 | 1・2着馬の**枠番**の組み合わせ（順不同）。海外開催では購入不可 |
| `QUINELLA` | 馬連 | 1・2着馬の馬番の組み合わせ（順不同） |
| `QUINELLAPLACE` | ワイド | 3着以内に入る2頭の組み合わせ（順不同） |
| `EXACTA` | 馬単 | 1・2着馬の馬番を着順通りに当てる |
| `TRIO` | 三連複 | 1・2・3着馬の馬番の組み合わせ（順不同） |
| `TRIFECTA` | 三連単 | 1・2・3着馬の馬番を着順通りに当てる |
| `WINPLACE` | 応援馬券 | 同一馬の単勝＋複勝のセット |

**応援馬券（`WINPLACE`）の注意点**

- 方式は**通常（`NORMAL`）のみ**、買い目は**1頭のみ**指定できます。
- **合計購入金額は指定金額の2倍**になります。点数も2点として数えます。
- 単勝と複勝の2点として購入されるため、**購入履歴には単勝と複勝が別々の馬券として現れます**（サイトで購入した場合と同じ挙動です）。
- 独立した2点として扱われるため、分割送信の境界で単勝と複勝が別チャンクに分かれ、通信失敗時に**片方だけが成立する可能性**があります。
- `GetOdds` にはこの式別を指定できません（`UNSUCCESS`）。単勝・複勝を個別に取得してください。

### Houshiki（方式）

| 値 | 方式 | 買い目の指定 |
|---|---|---|
| `NORMAL` | 通常 | 1点を指定 |
| `FORMATION` | フォーメーション | 各列に複数馬番 |
| `BOX` | ボックス | 1列に複数馬番（全組み合わせ） |
| `WHEEL_1ST` | 軸1頭ながし（1着流し） | `"軸-相手"` |
| `WHEEL_2ND` | 2着ながし（馬単・三連単） | `"軸-相手"` |
| `WHEEL_3RD` | 3着ながし（三連単） | `"軸-相手"` |
| `WHEEL_1ST_2ND` | 軸2頭ながし（三連複）／1・2着ながし（三連単） | 三連複`"軸,軸-相手"`／三連単`"1着軸-2着軸-相手"` |
| `WHEEL_1ST_3RD` | 1・3着ながし（三連単） | `"1着軸-相手-3着軸"` |
| `WHEEL_2ND_3RD` | 2・3着ながし（三連単） | `"相手-2着軸-3着軸"` |
| `WHEEL_MULTI_AXIS1` | 軸1頭ながしマルチ（馬単・三連単） | `"軸-相手"`（全着順） |
| `WHEEL_MULTI_AXIS2` | 軸2頭ながしマルチ（三連単のみ） | `"軸-軸-相手"`（全着順） |

> **ながし（`WHEEL_*`）／マルチ（`WHEEL_MULTI_*`）** は「軸」と「相手」を列（ハイフン区切り）で指定します。列の意味は式別・方式で変わります。マルチは馬単・三連単でのみ指定でき、`ST_BET_DATA.multi` が `1` に設定されます。

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
| `FAILED_OUT_OF_SERVICE` | 64 | サービス時間外（`Login` のみ。詳細は [Login](#login--ログイン) 参照） |

```csharp
uint ret = IpatHelper.Bet(new() { betData });

if ((ret & (uint)IpatHelper.RETURN_VALUE.SUCCESS) != 0)                 Console.WriteLine("購入成功");
if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_CHUOU) != 0)            Console.WriteLine("中央競馬での処理に失敗");
if ((ret & (uint)IpatHelper.RETURN_VALUE.FAILED_COMMUNICATE_CHUOU) != 0) Console.WriteLine("中央競馬との通信に失敗");
```

---

## 定数

引数の既定値や上限は `IpatHelper` の `public const` として公開しています。マジックナンバーの代わりに使ってください。

| 定数 | 値 | 内容 |
|---|---|---|
| `MAX_TOTAL_AMOUNT_PER_SEND` | 1000000 | 1回の送信あたりの合計購入金額の上限（円）。1点あたりの上限も同じ値 |
| `UMABAN_COLUMN_COUNT` | 3 | `ST_BET_DATA.horseNo` の要素数 |
| `UMABAN_TICKET_COLUMN_COUNT` | 5 | `ST_TICKET_DATA_DETAIL.horseNo` の要素数（WIN5 の5レース分を含む） |
| `WIN5_RACE_COUNT` | 5 | WIN5 のレース数 |
| `DEFAULT_RETRY_COUNT` | 10 | `Deposit` / `Withdraw` の既定リトライ回数 |
| `DEPOSIT_DEFAULT_VALUE` | 1000 | `SetAutoDepositFlag` の既定入金額（円） |
| `DEFAULT_CONFIRM_TIMEOUT` | 10000 | 残高反映を待つ既定のタイムアウト（ms） |
| `DEFAULT_BET_INTERVAL` | 500 | ネイティブ DLL 側の分割送信間隔の既定値（ms） |
| `DEFAULT_BET_INTERVAL_MANAGED` | 1000 | `Bet` / `BetWin5` が既定で渡す分割送信間隔（ms） |
| `MAX_WIN5_AUTO_BET_COUNT` | 50 | `BetWin5Auto` で生成させられる点数の上限 |

---

## 総合的な使用例

```csharp
using IpatHelperNet;

// ログイン
uint ret = IpatHelper.Login("1234567890", "12345678", "1234", "12345");
if ((ret & 1) != 1)
{
    // 時間外なら再試行しても無駄。ID・パスワード誤りとは扱いを分ける
    bool outOfService = (ret & (uint)IpatHelper.RETURN_VALUE.FAILED_OUT_OF_SERVICE) != 0;
    Console.WriteLine(outOfService ? "サービス時間外です" : "ログイン失敗");
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

    // 購入履歴を確認（中央・地方の両方が連結されて返る）
    if ((IpatHelper.GetPurchaseData(out var history) & 1) == 1)
    {
        Console.WriteLine($"残高: {history.balance} 円 / 当日購入: {history.dayPurchase} 円");

        foreach (var ticket in history.ticketData)
        foreach (var detail in ticket.detailData)
        {
            // 解析できなかった明細は買い目が入っていないので飛ばす
            if (detail.decisionFlag == (byte)IpatHelper.DECISIONFLAG.PARSE_FAILED) continue;

            // WIN5 は kaisai / raceNo に意味のある値が入らない
            if (detail.betFlag == (byte)IpatHelper.BET_FLAG.WIN5)
            {
                Console.WriteLine("WIN5");
                continue;
            }

            // 買い目の復元は「購入明細の読み方」を参照（列の意味が方式・式別で変わる）
            Console.WriteLine($"{(IpatHelper.Kaisai)detail.kaisai} {detail.raceNo}R " +
                              $"{(IpatHelper.Shikibetsu)detail.type} multi={detail.multi}");
        }
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
| `EntryPointNotFoundException: GetRaceCard` 等 | 出力フォルダの `IpatHelper.dll` が旧版です。パッケージを最新版に更新するか、`bin` を削除して再ビルドしてください。 |
| `Login` が `FAILED_OUT_OF_SERVICE` を返す | 投票受付時間外かメンテナンス中です（**地方競馬は営業時間外に必ずこの状態になります**）。即座のリトライは必ず失敗するため、時間をおいて再試行してください。 |
| ログインは成功するが購入で `FAILED_CHUOU` | 中央競馬にログインできていない可能性があります。`Login` の戻り値で `FAILED_CHUOU` を確認してください。 |
| オッズ/出馬表が `UNSUCCESS` | 指定した開催場がその日開催されていません。海外開催の場合は**中央競馬にログインできているか**も確認してください（枠連は海外非対応です）。 |
| 出馬表の騎手名・馬体重などが空 | 海外開催では I-PAT がこれらの項目を返しません（仕様）。取得できる項目は [GetRaceCard](#getracecard--出馬表取得) を参照してください。 |
| 購入履歴の買い目がおかしい | `horseNo[]` の列の意味は方式・式別で変わります。列を機械的に連結せず「[購入明細の読み方](#購入明細の読み方)」の対応表に従ってください。特に**枠連**と**マルチ**は要注意です。 |
| 入出金が `UNSUCCESS` になる | A-PAT 会員、または登録口座が PayPay（コード決済アプリ）の場合は仕様上利用できません。それ以外の場合は「[入出金が失敗したときの調べ方](#入出金が失敗したときの調べ方)」を参照してください。 |
| 文字化けする | 文字列は UTF-8 デコード済みで返ります。コンソール出力時は `Console.OutputEncoding = Encoding.UTF8;` を設定してください。 |
| ログのハンドラ内で処理が止まる | ハンドラから本クラスの API を呼び返すとデッドロックします。ハンドラ内では受け取ったメッセージを記録するだけにしてください。 |

---

## DLL を明示的にアンロードする場合

`NativeLibrary.Free` や `AssemblyLoadContext` のアンロードなどで **`IpatHelper.dll` を明示的に解放する場合は、次の順序を守ってください。**

1. **すべての API 呼び出しが戻っていること**を確認する（他スレッドで実行中の呼び出しが1つも無い状態にする）
2. `IpatHelper.Logout()` を呼ぶ
3. `IpatHelper.SetLogCallback(null)` を呼んでコールバックを解除する
4. アンロードする

- 実行中の呼び出しが残ったままアンロードすると動作は未定義です。
- `SetLogCallback(null)` から戻った時点で実行中のハンドラは存在しないことが保証されます。解除せずにアンロードすると、解放済みのコードへコールバックする可能性があります。
- `Logout` を省略してもローカルの後始末は行われますが、サーバ側のセッションはログアウトされないまま残ります。

> **プロセス終了時にこの手順は不要です。** 通常のアプリケーション終了では OS がまとめて解放します。

---

## 開発者向け: リリース手順

公開は GitHub Actions が自動で行います。**手元での公開作業は不要**です
（`publish.bat` は廃止しました）。

### リポジトリ構成

```
IpatHelperNet/
├── .github/workflows/release.yml ... バージョン更新・パック・NuGet 公開を行う
├── README.md                ... 本ファイル。パッケージの README として同梱される
├── IpatHelperNet.slnx
└── IpatHelperNet/
    ├── IpatHelperNet.csproj ... <Version> を release.yml が自動で更新する
    ├── IpatHelper.cs        ... P/Invoke ラッパー本体
    ├── runtimes/
    │   ├── win-x64/native/IpatHelper.dll
    │   └── win-x86/native/IpatHelper.dll
    └── nupkg/               ... 生成された .nupkg（git 管理外）
```

### 自動公開の流れ

[IpatHelperNative](https://github.com/yawatamikiya/IpatHelperNative) の CI がビルドした
`IpatHelper.dll` を `IpatHelperNet/runtimes/` 配下へ push してくると、それを合図に
`release.yml` が起動して次を順に行います。

1. `IpatHelperNet/IpatHelperNet.csproj` からバージョンを読み取り、**パッチ番号を +1** して書き戻す
2. `dotnet pack -c Release` で `IpatHelperNet/nupkg/` へパッケージを作成
3. `dotnet nuget push` で nuget.org へ公開
4. バージョン更新をコミットして `main` へ push する

DLL のパスが変わったときだけ起動するため、他のコミットでは公開は走りません。
NuGet の API キーはリポジトリの secret `NUGET_API_KEY` から取得します。

> [!NOTE]
> `README.md` の一時コピーは不要になったため廃止しました。`.csproj` の
> `<None Include="..\README.md" Pack="true" PackagePath="\" />` が
> パッケージ直下へ収めるため、コピーしなくても README・ネイティブ DLL とも正しく同梱されます。

### 手動で公開する

DLL を更新していないタイミングで公開したい場合は、GitHub の Actions タブから
`Release to NuGet` を `workflow_dispatch` で実行します。

`dryrun` を `true` にすると、バージョン更新も公開も行わず、パッケージ作成までを確認できます。
作成された `.nupkg` は実行結果の Artifacts からダウンロードできます。

> [!WARNING]
> **`.csproj` の `<Version>` を手で上げないでください。** CI が +1 するため、
> 手動でも上げると番号が飛びます。また NuGet は同一バージョンの再アップロードを
> 拒否するため、`.csproj` の値は nuget.org の公開済み最新版と一致している必要があります。

---

## 注意事項

- 本ライブラリは **Windows 専用**です。
- 本ライブラリを使用した馬券購入は**実際の金銭を伴います**。十分にテストしてからご使用ください。
- I-PAT の仕様変更により動作しなくなる場合があります。
- ログイン情報（ID・パスワード・P-ARS 番号）はメモリ上にのみ保持され、ファイルへの保存は行いません。
- 全 API はスレッドセーフです。ただし**通信を伴う API は直列化される**ため、先行する呼び出しが完了するまでブロックします（投票・入出金は通信と残高反映待ちを含むため、数分に及ぶことがあります）。`GetBetInstance` / `GetBetInstanceWin5` は通信を行わないため、他の API の実行中でも並行して呼び出せます。
- ネイティブ側の例外がマネージド側へ伝播することはありません。内部で発生した例外はすべて捕捉され `UNSUCCESS` として返ります。

---

## ライセンス

MIT License
