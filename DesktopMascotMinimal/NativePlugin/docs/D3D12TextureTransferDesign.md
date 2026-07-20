# D3D12 Texture Transfer Design

## 1. 目的と範囲

この文書は、Unity 6000.3.20f1 / Windows x64 / Direct3D 12で、
Unity管理の`RenderTexture`を将来ネイティブ側のテクスチャへコピーするための
API、スレッド、所有権、リソース状態、同期方式を確定する。

現段階では次を実装しない。

- `Texture.GetNativeTexturePtr()`による実ポインター受け渡し
- D3D12コマンドアロケーターまたはコマンドリストの作成
- コピーコマンドまたはリソースバリアの記録
- queueへのコマンド投入
- GPU待機
- DirectComposition、composition swap chain、Win32ウィンドウ

調査の第一資料は、Unity 6000.3.20f1からコピーした
[`IUnityGraphicsD3D12.h`](../unity/IUnityGraphicsD3D12.h)である。
インストール元は次の場所である。

```text
C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Data\PluginAPI
```

## 2. 使用するインターフェース

使用するインターフェースは`IUnityGraphicsD3D12v8`である。

```cpp
UNITY_REGISTER_INTERFACE_GUID(
    0x9d303045d00d4cfdULL,
    0x8febb42968b423b6ULL,
    IUnityGraphicsD3D12v8)
```

コピー済みヘッダーではv8が最新である。GUID行にはUnity自身の
`TODO: Get proper values`コメントが残っているが、実行中のUnity
6000.3.20f1からこのGUIDでv8を取得できることは確認済みである。

### 2.1 継承関係

`IUnityGraphicsD3D12v8`は、C++の型としてv7以前を継承していない。
各バージョンは`IUnityInterface`から派生する独立した構造体であり、
それぞれ異なるGUIDを持つ。v8は旧版のメソッドを再宣言した累積的な
インターフェースである。

したがって、v7ポインターをv8へcastすることや、v8取得失敗時に
`reinterpret_cast`で旧版を偽装することは禁止する。

### 2.2 バージョンごとの追加・変更

| バージョン | ヘッダー上の主な内容 |
|---|---|
| obsolete基本版 | `GetDevice`、`GetCommandQueue`、frame fence、`GetResourceState`、`SetResourceState` |
| v2 | `ExecuteCommandList`と`UnityGraphicsD3D12ResourceState`を導入。obsolete版のstate APIとqueue APIは含まない |
| v3 | `SetPhysicalVideoMemoryControlValues`を追加 |
| v4 | `GetCommandQueue`を追加 |
| v5 | `TextureFromRenderBuffer`を追加 |
| v6 | `TextureFromNativeTexture`、`ConfigureEvent`、`CommandRecordingState`を追加 |
| v7 | Player swap chain、sync interval、present flagsを追加 |
| v8 | `RequestResourceState`と`NotifyResourceState`を追加 |

obsolete基本版だけが明示的に`// Obsolete`と記載されている。
v2からv7は旧版だが、個別のメソッドにdeprecated属性は付いていない。
新規実装ではv8だけを使用する。

## 3. v8の全メソッド

`IUnityGraphicsD3D12v8`に存在するメソッドは次の15個である。

```cpp
ID3D12Device* GetDevice();
IDXGISwapChain* GetSwapChain();
UINT32 GetSyncInterval();
UINT GetPresentFlags();
ID3D12Fence* GetFrameFence();
UINT64 GetNextFrameFenceValue();
UINT64 ExecuteCommandList(
    ID3D12GraphicsCommandList* commandList,
    int stateCount,
    UnityGraphicsD3D12ResourceState* states);
void SetPhysicalVideoMemoryControlValues(
    const UnityGraphicsD3D12PhysicalVideoMemoryControlValues* memInfo);
ID3D12CommandQueue* GetCommandQueue();
ID3D12Resource* TextureFromRenderBuffer(UnityRenderBuffer rb);
ID3D12Resource* TextureFromNativeTexture(UnityTextureID texture);
void ConfigureEvent(
    int eventID,
    const UnityD3D12PluginEventConfig* pluginEventConfig);
bool CommandRecordingState(
    UnityGraphicsD3D12RecordingState* outCommandRecordingState);
void RequestResourceState(
    ID3D12Resource* resource,
    D3D12_RESOURCE_STATES state);
void NotifyResourceState(
    ID3D12Resource* resource,
    D3D12_RESOURCE_STATES state,
    bool UAVAccess);
```

ヘッダーはインターフェース全体について、rendering threadまたは
submission threadでのみ使用するよう指定している。

## 4. 関連構造体

### 4.1 UnityGraphicsD3D12ResourceState

```cpp
typedef struct UnityGraphicsD3D12ResourceState
{
    ID3D12Resource* resource;
    D3D12_RESOURCE_STATES expected;
    D3D12_RESOURCE_STATES current;
} UnityGraphicsD3D12ResourceState;
```

- `resource`: barrier対象
- `expected`: `ExecuteCommandList`が実行される直前に期待する状態
- `current`: そのコマンドリスト実行後の状態

これはプラグイン所有コマンドリストを`ExecuteCommandList`へ渡す方式で、
Unityへ前後の状態を申告するための構造体である。

### 4.2 UnityGraphicsD3D12RecordingState

```cpp
typedef struct UnityGraphicsD3D12RecordingState
{
    ID3D12GraphicsCommandList* commandList;
} UnityGraphicsD3D12RecordingState;
```

`commandList`はUnityが現在記録しているUnity所有コマンドリストである。
プラグインは借用するだけであり、次を行ってはならない。

- `AddRef`または`Release`
- `Close`または`Reset`
- `ExecuteCommandLists`
- `ComPtr`による所有
- callback終了後の保持または再利用

### 4.3 UnityGraphicsD3D12PhysicalVideoMemoryControlValues

```cpp
typedef struct UnityGraphicsD3D12PhysicalVideoMemoryControlValues
{
    UINT64 reservation;
    UINT64 systemMemoryThreshold;
    UINT64 residencyHysteresisThreshold;
    float nonEvictableRelativeThreshold;
} UnityGraphicsD3D12PhysicalVideoMemoryControlValues;
```

Unityの物理ビデオメモリー制御値を変更する構造体であり、
テクスチャ転送には使用しない。

### 4.4 イベント設定

```cpp
typedef struct UnityD3D12PluginEventConfig
{
    UnityD3D12GraphicsQueueAccess graphicsQueueAccess;
    UINT32 flags;
    bool ensureActiveRenderTextureIsBound;
} UnityD3D12PluginEventConfig;
```

`ConfigureEvent`は初期化中に呼ぶ。

`graphicsQueueAccess`の意味は次のとおりである。

| 値 | callback thread | 使用できるAPI | 使用できないAPI |
|---|---|---|---|
| `kUnityD3D12GraphicsQueueAccess_DontCare` | render thread | `CommandRecordingState` | `GetCommandQueue` |
| `kUnityD3D12GraphicsQueueAccess_Allow` | submission thread | `GetCommandQueue` | `CommandRecordingState` |

ヘッダーは、無調整に両方へアクセスすると、Unityのrender threadまたは
submission threadと競合して問題を起こす可能性が高いと明記している。

イベントフラグは次のとおりである。

- `kUnityD3D12EventConfigFlag_EnsurePreviousFrameSubmission`
  - ヘッダー上で`NOT SUPPORTED`
- `kUnityD3D12EventConfigFlag_FlushCommandBuffers`
  - 既存command bufferをsubmitする
- `kUnityD3D12EventConfigFlag_SyncWorkerThreads`
  - worker thread完了を待つ
- `kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState`
  - descriptor、vertex bufferなどのcommand buffer状態を変更する場合に設定
  - ヘッダー上のdefaultはset

## 5. APIごとの用途・所有権・同期

### 5.1 デバイス、queue、frame fence

| API | 用途・戻り値 | 所有権 | 同期上の扱い |
|---|---|---|---|
| `GetDevice()` | Unityの`ID3D12Device*` | Unity所有。借用のみ | rendering/submission thread限定 |
| `GetCommandQueue()` | UnityのDirect `ID3D12CommandQueue*` | Unity所有。借用のみ | `graphicsQueueAccess=Allow`のsubmission-thread callbackでのみ実作業に使う |
| `GetFrameFence()` | Unityの`ID3D12Fence*` | Unity所有。借用のみ | プラグインがRelease、Signal、所有権移譲しない |
| `GetNextFrameFenceValue()` | 現在フレーム完了またはGPU flush時にframe fenceへ設定される値 | 値 | frame完了確認用。CPUを毎フレームblockする設計には使わない |

ヘッダーはCOM参照カウントの契約を明文化していない。このプロジェクトでは
Unityインターフェースから返るD3D12オブジェクトをUnity所有として扱い、
`AddRef`、`Release`、所有`ComPtr`への格納を禁止する。

### 5.2 Unityの表示情報

| API | 用途 |
|---|---|
| `GetSwapChain()` | Unity Playerのswap chain。Editorでは`nullptr` |
| `GetSyncInterval()` | Unity Playerのpresent sync interval。Editorでは`0` |
| `GetPresentFlags()` | Unity Playerのpresent flags。Editorでは`0` |

将来作成するDirectComposition swap chainはUnity Playerのswap chainとは別物である。
これら3 APIは今回のコピー経路には使用しない。

### 5.3 テクスチャ・リソースアクセス

| API | 引数 | 戻り値と所有権 | 用途 |
|---|---|---|---|
| `TextureFromRenderBuffer` | `UnityRenderBuffer` | Unity所有`ID3D12Resource*` | Unity内部のrender buffer handleをD3D12 resourceへ変換 |
| `TextureFromNativeTexture` | `UnityTextureID` | Unity所有`ID3D12Resource*` | Unityの`UnityTextureID`をD3D12 resourceへ変換 |
| `RequestResourceState` | resource、要求state | なし | active Unity command list内で必要ならbarrierを追加 |
| `NotifyResourceState` | resource、最終state、UAV access有無 | なし | プラグイン記録後のstateをUnity backendへ通知 |

`TextureFromRenderBuffer`へ渡す`UnityRenderBuffer`は
`RenderSurfaceBase*`であり、C#の`RenderTexture`から直接得られる値ではない。

`TextureFromNativeTexture`の引数`UnityTextureID`はコピー済み
`IUnityInterface.h`で`unsigned int`として定義されている。
D3D12の`Texture.GetNativeTexturePtr()`が返す64-bitポインターを
`UnityTextureID`へcastまたは切り詰めて渡してはならない。

### 5.4 Unity command listへ記録するAPI

| API | 引数・戻り値 | 所有権・責任 |
|---|---|---|
| `CommandRecordingState` | out構造体。利用可能なら`true` | command listはUnity所有。プラグインはcallback中だけ借用 |
| `RequestResourceState` | resourceと要求state | Unityがactive listの状態追跡に基づき必要なbarrierを追加 |
| `NotifyResourceState` | resource、最終state、UAV access | 以降のUnity commandが正しいbarrierを追加できるよう状態を申告 |

正確なメソッド名は`CommandRecordingState`である。
`GetCommandRecordingState`というメソッドは存在しない。

### 5.5 プラグイン所有command listを実行するAPI

```cpp
UINT64 ExecuteCommandList(
    ID3D12GraphicsCommandList* commandList,
    int stateCount,
    UnityGraphicsD3D12ResourceState* states);
```

- command list typeは`D3D12_COMMAND_LIST_TYPE_DIRECT`でなければならない。
- optionalのstate配列で実行前後のresource stateを申告できる。
- Unityがworker thread上で実行する。
- 戻り値はframe完了またはGPU flush時に設定されるfence valueである。
- command allocator、command list、そのreset/reuse同期はプラグインの責任である。
- Unity frame fence自体はUnity所有である。

これは正式APIだが、最初のコピー実装には採用しない。

### 5.6 存在確認

| 要求された名称 | v8での結果 |
|---|---|
| `AccessTexture` | 存在しない |
| `AccessBuffer` | 存在しない |
| `TextureFromRenderBuffer` | 存在する |
| `TextureFromNativeTexture` | 存在する |
| `GetResourceState` | v8には存在しない。obsolete基本版だけ |
| `SetResourceState` | v8には存在しない。obsolete基本版だけ |
| `BeginFrame` | 存在しない |
| `EndFrame` | 存在しない |
| `ExecuteCommandList` | 存在する |
| `GetCommandRecordingState` | 存在しない |
| `CommandRecordingState` | 存在する |
| `UnityGraphicsD3D12RecordingState` | 存在する |
| `UnityGraphicsD3D12ResourceState` | 存在する |
| `UnityGraphicsD3D12PhysicalVideoMemoryControlValues` | 存在する |

## 6. Texture.GetNativeTexturePtr

Unity 6.0公式ドキュメントは、D3D12で
`Texture.GetNativeTexturePtr()`が返す値を`ID3D12Resource*`と明記している。

- Managed型: `IntPtr`
- D3D12ネイティブ型: `ID3D12Resource*`
- 所有権: Unity所有として借用する
- `AddRef`/`Release`しない
- texture再作成、resize、またはUnity APIによるpixel data変更後は
  native pointerが変化し得るため再取得する
- multithreaded renderingではrender threadとの同期が発生する遅いAPIなので、
  毎フレーム呼ばず初期化時または再作成時だけ取得する

将来はmanaged側で`RenderTexture`への強参照を保持し、取得したポインターを
イベントデータとしてrender-thread callbackへ一時的に渡す。ネイティブ側で
長期保存しない。

候補となる正式な受け渡しAPIは
`CommandBuffer.IssuePluginEventAndData(IntPtr callback, int eventID, IntPtr data)`
と、`IUnityGraphics.h`の次のcallback型である。

```cpp
typedef void (
    UNITY_INTERFACE_API * UnityRenderingEventAndData)(
        int eventId,
        void* data);
```

この方式なら、ポインターをC# main threadから直接使用せず、Unityの
command streamに配置されたrender-thread callbackで消費できる。

## 7. 採用するコピー方式

### 7.1 結論

最初のコピー方式はAを採用する。

> A. Unityのrecording stateからcommand listを取得し、Unity所有command
> listへコピーを記録する。

ただし、単にcommand listを取得するだけではなく、v8で追加された
`RequestResourceState`と`NotifyResourceState`を必ず組み合わせる。

### 7.2 想定手順

将来のコピーイベントを初期化中に次の方針で設定する。

```text
graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare
callback thread     = render thread
queue access        = disabled
recording state     = enabled
```

render event callbackでは次の順序を採用する。

1. event dataから、そのcallback中だけ有効なUnity所有
   `ID3D12Resource*`を受け取る。
2. `CommandRecordingState(&state)`を呼ぶ。
3. `false`または`state.commandList == nullptr`なら、そのframeのコピーを
   安全にskipする。
4. sourceとdestinationの`ID3D12Resource::GetDesc()`を検証する。
5. Unity管理sourceに
   `RequestResourceState(source, D3D12_RESOURCE_STATE_COPY_SOURCE)`を要求する。
6. destinationに
   `RequestResourceState(destination, D3D12_RESOURCE_STATE_COPY_DEST)`を要求する。
7. 同一サイズ・同一sample構成・copy-compatible formatの場合だけ、
   Unity所有command listへコピーを記録する。
8. 記録後にsourceとdestinationの最終stateを
   `NotifyResourceState`でUnityへ通知する。
9. command listをclose、execute、release、callback外へ保存しない。

実際のコピーAPIは、resource全体の条件が一致する場合は`CopyResource`、
部分コピーやsubresource指定が必要な場合は`CopyTextureRegion`を候補とする。
どちらもformat変換、RGBA/BGRA変換、alpha premultiplicationは行わない。

### 7.3 この方式を選ぶ理由

- 既に確認済みのrender-thread callbackをそのまま使用できる。
- 独自allocator、command list、queue submit、fenceをまだ必要としない。
- Unityのcommand stream内の順序へコピーを配置できる。
- Unity管理sourceの不明な直前stateをプラグインが推測せずに済む。
- v8固有の`RequestResourceState`/`NotifyResourceState`を利用できる。
- Unityがcommand listのsubmitとframe fenceを管理する。

## 8. 採用しない方式

### 8.1 B: 独自command list + ExecuteCommandList

`ExecuteCommandList`はv8の正式APIなので禁止APIではない。ただし最初の
コピー実装には採用しない。

理由:

- allocatorとcommand listの所有・reset・再利用同期が必要
- `UnityGraphicsD3D12ResourceState`配列の正確な維持が必要
- 戻りfence valueとallocator再利用の同期が必要
- Aより実装範囲が大きく、現段階の最小実装順序に合わない

将来、Unityのactive recording listへ記録できないケースが確認された場合の
正式なfallback候補とする。

### 8.2 C: Unity queueへ直接ExecuteCommandLists

現在のコピー方式では採用しない。

`GetCommandQueue`自体はv8の正式APIだが、render-thread callbackから
直接`ExecuteCommandLists`するのは危険である。ヘッダーは、Unityの
submission threadが同じqueueへアクセスしている可能性が高く、競合すると
明記している。

直接queueを使用できるのは、イベントを
`kUnityD3D12GraphicsQueueAccess_Allow`で構成し、Unityがcallbackを
submission thread上で実行している場合に限定する。その場合は
`CommandRecordingState`が無効になるため、Aと同一イベントで併用できない。

無調整な直接投入では次の保証を失う。

- Unity描画command listとのsubmit順序
- resource state trackerとの整合性
- Unity frame fence値との対応
- Graphics Jobsおよびworker threadとの同期
- allocatorとcommand listの安全な再利用時点

従って「取得済みqueue pointerがある」ことは、任意のthreadから自由に
submitしてよいことを意味しない。

## 9. リソース状態管理

### 9.1 Unity管理RenderTexture

想定sourceはUnityが描画を完了した`RenderTexture`である。
直前stateを`D3D12_RESOURCE_STATE_RENDER_TARGET`と決め打ちしてはならない。
Unity backendのpass構成によって、shader resource、resolve source、
common、その他の追跡済みstateである可能性がある。

責任分担:

- プラグイン: 必要stateとして`COPY_SOURCE`を要求
- Unity v8 backend: 現在の追跡stateから必要なbarrierをactive listへ追加
- プラグイン: コピー後のstateを`NotifyResourceState`で申告
- Unity: 後続のUnity利用時に必要なbarrierを追加

sourceを手書きの`ResourceBarrier(RENDER_TARGET -> COPY_SOURCE)`で
遷移させない。

### 9.2 プラグイン所有中間テクスチャ

destinationは同じUnity `ID3D12Device`で作成する。
初期stateと以後のstateはプラグインが追跡する。

最初の候補:

```text
初期状態または前回状態 -> COPY_DEST
コピー完了後           -> COPY_DESTのまま、または次の利用状態
```

active Unity command listへ記録する場合は、destinationについても
`RequestResourceState`/`NotifyResourceState`を使用し、Unity側の
command-list state trackerと矛盾させない。

### 9.3 将来のcomposition swap chain back buffer

D3D12 swap chain back bufferは`COMMON/PRESENT`から開始する。
将来直接back bufferへコピーする場合の概念的な状態は次のとおりである。

```text
PRESENT/COMMON
  -> COPY_DEST
  -> copy
  -> PRESENT/COMMON
  -> Present
```

MicrosoftのD3D12資料では、Present前にback bufferが
`COMMON`である必要があり、`PRESENT`は`COMMON`の別名である。

composition swap chainはプラグイン所有なので、そのback bufferの状態追跡と
Present前の復帰はプラグインの責任である。ただし、Unity所有command listへ
barrierを記録する場合はv8 state trackerとの整合も維持する。

## 10. フェンスと同期責任

採用方式Aでは、コピーcommand自体のsubmitとGPU順序はUnityのcommand listと
frame fenceへ委ねる。

- 独自fenceを作らない。
- Unity frame fenceをReleaseまたはSignalしない。
- render event callback内でCPU waitしない。
- destinationをCPUまたは別queueから利用する前にGPU完了保証が必要な場合は、
  `GetFrameFence()`と対応するframe fence valueを利用する設計を検討する。
- 毎フレームCPUをblockする方式は採用しない。
- 複数frameを安全に重ねるため、将来はdestinationのdouble/triple bufferingを
  検討する。

DirectComposition swap chainのPresentをUnityのcopy submitより後へ確実に
並べる方法は未解決事項である。Aで記録したcommand listはcallback終了後に
Unityがsubmitするため、callback中に先に`Present`を呼んではならない。

候補は次のいずれかであり、次段階以降で実測する。

1. Unity submission完了後にPresentできる正式なevent配置を使用する。
2. submission-thread eventを`Allow`で別途構成し、queue順序をUnityと調整する。
3. `ExecuteCommandList`とUnity frame fenceを使う方式へ切り替える。

## 11. RenderTexture形式とalpha

### 11.1 第一候補

最初に検証する形式は次のとおりである。

```text
GraphicsFormat.B8G8R8A8_SRGB
antiAliasing = 1
alpha channel = 8 bit
```

理由:

- BGRA 8:8:8:8でDirectComposition向け
  `DXGI_FORMAT_B8G8R8A8_UNORM`と同じchannel配置
- alpha channelを保持
- Linear color spaceでrender時のLinear-to-sRGB変換を利用できる
- RGBAからBGRAへの変換passを避けられる可能性が高い

実機では必ず次を確認する。

```csharp
SystemInfo.IsFormatSupported(
    GraphicsFormat.B8G8R8A8_SRGB,
    FormatUsage.Render)
```

また、取得した`ID3D12Resource::GetDesc().Format`を診断し、
composition back bufferとのD3D12 copy compatibilityを確認する。

### 11.2 fallback候補

- `GraphicsFormat.B8G8R8A8_UNorm`
  - 明示的にLinear/sRGB変換を制御する場合
- `GraphicsFormat.R8G8B8A8_SRGB`
  - BGRA render-target supportがない場合
  - composition用BGRAへのshader変換が必要
- `GraphicsFormat.R8G8B8A8_UNorm`
  - 明示的な色変換passを行う場合

`CopyResource`と`CopyTextureRegion`はRGBA/BGRA変換をしないため、
RGBA fallbackをそのままBGRA swap chainへコピーしない。

MSAA RenderTextureは非MSAA destinationへ単純コピーできないため、
最初の転送用RenderTextureは`antiAliasing = 1`とする。

### 11.3 sRGBとLinear

Unityの`RenderTexture.sRGB`は、Linear color space使用時にrender時の
Linear-to-sRGB、shader sampling時のsRGB-to-Linear変換を行うかを示す。
`GraphicsFormat`の`SRGB`形式ではRGBだけがsRGB非線形符号化され、
alphaは通常のUNormである。

sourceとdestinationのbit表現が一致しない限り、コピーAPIだけで色空間変換は
行われない。

### 11.4 premultiplied alpha

DirectComposition用swap chainは
`DXGI_ALPHA_MODE_PREMULTIPLIED`を採用する。
このモードではRGBが既にalpha倍されていなければならない。

推奨方針:

1. Unity側の最終出力passで、linear RGBをalpha倍する。
2. 必要なsRGB encodingを行ってBGRA8 RenderTextureへ保存する。
3. 透明領域を`(0, 0, 0, 0)`でclearする。
4. premultiplied済みのbit列をcopyでそのまま転送する。

コピー後にalpha premultiplicationを行うことはできない。
既存Unity出力がstraight alphaなら、将来shaderまたはcompute passを追加する。
CPU変換は行わない。

## 12. 将来のDirectComposition接続点

Microsoftの`IDXGIFactory2::CreateSwapChainForComposition`では、D3D12の場合
`pDevice`引数にDirect command queueを渡す。現在取得済みのUnity Direct
queueがこの要件を満たす。

将来の接続候補:

- Unity所有`ID3D12Device`
- Unity所有Direct `ID3D12CommandQueue`
- `CreateSwapChainForComposition`
- `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`
- `DXGI_SCALING_STRETCH`
- `DXGI_FORMAT_B8G8R8A8_UNORM`
- `DXGI_ALPHA_MODE_PREMULTIPLIED`
- `IDCompositionVisual::SetContent`

Unity所有queueは借用のままとし、プラグインがReleaseしない。
swap chain、DirectComposition device、target、visualなど、
プラグイン自身が生成したCOMオブジェクトだけを`ComPtr`で所有する。

## 13. 次段階の最小実装

次段階ではまだコピーせず、次の診断だけを実装する。

1. 転送専用event IDを追加する。
2. `ConfigureEvent`でそのeventを`DontCare`に設定する。
3. managed側で`B8G8R8A8_SRGB`、MSAAなしの小さいRenderTextureを作る。
4. format supportと実際の`graphicsFormat`をPlayer.logへ記録する。
5. `GetNativeTexturePtr()`を作成時に1回だけ呼ぶ。
6. `CommandBuffer.IssuePluginEventAndData`でポインターを一時的に渡す。
7. native callbackでポインターを`ID3D12Resource*`として一時参照する。
8. `GetDesc()`のdimension、width、height、format、sample countだけを記録する。
9. `CommandRecordingState`が`true`か、command listが非nullかだけを記録する。
10. pointer、command list、resourceを保存、AddRef、Releaseしない。
11. barrier、copy、execute、fence待機は行わない。

この診断が成功してから、別の段階で初めてstate requestとcopyを追加する。

## 14. 未解決事項

- `B8G8R8A8_SRGB`の実機Render用途support
- Unityが生成するD3D12 resourceの実際の`DXGI_FORMAT`
- Unity sourceとcomposition back bufferのcopy compatibility
- Unity RenderTexture再作成、resize、device reset時のpointer更新手順
- `CommandRecordingState`が現在のGraphics Jobs構成で常に利用可能か
- active recording listでplugin所有resourceへ
  `RequestResourceState`した場合の実測動作
- Aで記録したcopy後にcomposition swap chainをPresentする正しいCPU時点
- Unity frame fenceを用いたnon-blockingなdestination再利用方法
- double bufferとtriple bufferのどちらを採用するか
- Unity出力がstraight alphaかpremultiplied alphaかの画素検証
- HDR、10-bit、色管理を将来サポートするか
- `TextureFromNativeTexture(UnityTextureID)`へ渡す正式な
  `UnityTextureID`取得経路。D3D12 pointerのcastでは代用しない

## 15. 公式資料

- [Unity 6.0: Texture.GetNativeTexturePtr](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture.GetNativeTexturePtr.html)
- [Unity 6.0: GraphicsFormat](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Experimental.Rendering.GraphicsFormat.html)
- [Unity 6.0: RenderTextureDescriptor.graphicsFormat](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTextureDescriptor-graphicsFormat.html)
- [Unity 6.0: RenderTexture.sRGB](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTexture-sRGB.html)
- [Unity 6.0: SystemInfo.IsFormatSupported](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SystemInfo.IsFormatSupported.html)
- [Unity 6.0: CommandBuffer.IssuePluginEventAndData](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.CommandBuffer.IssuePluginEventAndData.html)
- [Microsoft: CreateSwapChainForComposition](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgifactory2-createswapchainforcomposition)
- [Microsoft: DXGI_ALPHA_MODE](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/ne-dxgi1_2-dxgi_alpha_mode)
- [Microsoft: D3D12_RESOURCE_STATES](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_resource_states)
- [Microsoft: Using resource barriers](https://learn.microsoft.com/en-us/windows/win32/direct3d12/using-resource-barriers-to-synchronize-resource-states-in-direct3d-12)
