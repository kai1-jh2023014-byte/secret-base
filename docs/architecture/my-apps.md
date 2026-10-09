# My Apps（マイアプリ）

Secret Base に **自分のアプリ / フォルダ / Web** を登録して、オーバーレイから起動するためのウィジェットです。  
Shell・管理者権限・プラグイン実行権は付与しません。登録は「起動先のメモ」だけです。

> English summary: My Apps is an allowlisted launcher registry (`apps.json`). Types are Application / Folder / Website. Launch goes through `ITargetLaunchService` / `ICursorLaunchService` only — never arbitrary shell.

## 使い方（UI）

1. 左下 **+** → Add Widget → **My Apps**
2. ウィジェット内 **Add / Register** を開く
3. 入力する項目:

| 項目 | 必須 | 説明 |
|------|------|------|
| **Name** | ✅ | 表示名（最大 80 文字） |
| **Description** | | 任意メモ（最大 280 文字） |
| **Type** | ✅ | `Application` / `Folder` / `Website` |
| **Launch target** | ✅ | 起動先（下表） |
| **Project root** | | Cursor で開く用のフォルダ（任意） |

4. **Open** … 登録先を起動  
5. **Cursor** … `Project root`（または Folder 型の target）を Cursor で開く  
6. **×** … **登録だけ削除**（実ファイルは消しません）

## 接続できる形式（Launch target）

| Type | 例 | ルール |
|------|----|--------|
| **Application** | `C:\Games\PokemonCalc.exe` | 絶対パスの実行ファイル。Block と同じパス検証。引数・Shell・`.bat` の自由実行は不可 |
| **Folder** | `C:\src\my-project` | 絶対パスのフォルダ。Explorer / Finder 相当で開く |
| **Website** | `https://classroom.google.com/` | `http` / `https` のみ。危険スキームは拒否 |

補足:

- ディスク上にファイルが無くても登録は残ります（起動時にホストが “not found” を出します）
- Creative Projects（制作記録）とは別物です。任意で `creativeProjectId` / `projectRoot` を紐づけ可能
- Base AI の `apps_open` も **この allowlist だけ** を開けます

## 保存場所

| OS | Path |
|----|------|
| Windows | `%LocalAppData%\SecretBase\apps\apps.json` |
| macOS | `~/Library/Application Support/SecretBase/apps/apps.json` |

書き込みは atomic: `.tmp` → コピー → `.tmp` 削除。

## `apps.json` スキーマ（schemaVersion 1）

```json
{
  "schemaVersion": 1,
  "apps": [
    {
      "id": "a1b2c3d4e5f60718293a4b5c6d7e8f90",
      "name": "Pokemon Calculator",
      "description": "Damage calc",
      "launchTarget": "C:\\Apps\\PokemonCalc.exe",
      "type": 0,
      "projectRoot": "C:\\src\\pokemon-calc",
      "creativeProjectId": null,
      "dateAdded": "2026-10-08T00:00:00+00:00"
    },
    {
      "id": "11223344556677889900aabbccddeeff",
      "name": "Classroom",
      "description": null,
      "launchTarget": "https://classroom.google.com/",
      "type": 2,
      "projectRoot": null,
      "creativeProjectId": null,
      "dateAdded": "2026-10-08T00:00:00+00:00"
    }
  ]
}
```

### `type` 列挙

| 値 | 意味 |
|----|------|
| `0` | Application |
| `1` | Folder |
| `2` | Website |

### フィールド詳細

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | 32 hex (`Guid` N). 自動採番 |
| `name` | string | 必須・trim・≤80 |
| `description` | string? | ≤280 |
| `launchTarget` | string | Application/Folder = 正規化済み絶対パス / Website = 正規化 URL |
| `type` | number | 上記 enum |
| `projectRoot` | string? | Cursor 用フォルダ。絶対パス |
| `creativeProjectId` | string? | Creative Project への任意リンク（≤64） |
| `dateAdded` | string | ISO-8601 |

## コマンド層

`AppCommandService`:

| Command | 効果 |
|---------|------|
| `ListApps` | 登録一覧 |
| `OpenApp` | ホストに launch 依頼（実 Process は Platform） |
| `OpenAppInCursor` | `projectRoot` を Cursor で開く |
| `RemoveApp` | レジストリから削除のみ |

AI / Integration Hub からも同じコマンド経由です。WebView Host Bridge や任意シェルは使いません。

## 関連

- [Integration Hub](integration-hub.md)
- [Security boundaries](security-boundaries.md)
- [Assistant Tools](assistant-tools.md)（`apps_open`）
