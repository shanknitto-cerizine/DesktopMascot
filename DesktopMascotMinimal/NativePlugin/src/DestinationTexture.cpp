#include "DesktopMascotNative/DestinationTexture.h"

#include <Windows.h>
#include <d3d12.h>
#include <wrl/client.h>

#include <atomic>
#include <mutex>
#include <utility>

namespace
{
    using Microsoft::WRL::ComPtr;

    constexpr UINT64 kDestinationWidth = 256;
    constexpr UINT kDestinationHeight = 256;
    constexpr UINT16 kDestinationDepthOrArraySize = 1;
    constexpr UINT16 kDestinationMipLevels = 1;
    constexpr DXGI_FORMAT kDestinationFormat =
        DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
    constexpr D3D12_RESOURCE_STATES kDestinationInitialState =
        D3D12_RESOURCE_STATE_COPY_DEST;

    std::mutex g_destinationTextureMutex;
    ComPtr<ID3D12Resource> g_destinationTexture;
    std::atomic<bool> g_destinationTextureAvailable{false};
    std::atomic<std::int32_t> g_destinationTextureCreationResult{
        DesktopMascotNative::kDestinationTextureCreationNotAttempted};
    std::atomic<std::int32_t> g_destinationTextureDimension{0};
    std::atomic<std::uint64_t> g_destinationTextureWidth{0};
    std::atomic<std::uint32_t> g_destinationTextureHeight{0};
    std::atomic<std::uint32_t> g_destinationTextureDepthOrArraySize{0};
    std::atomic<std::uint32_t> g_destinationTextureMipLevels{0};
    std::atomic<std::int32_t> g_destinationTextureFormat{0};
    std::atomic<std::uint32_t> g_destinationTextureSampleCount{0};
    std::atomic<std::uint32_t> g_destinationTextureSampleQuality{0};
    std::atomic<std::int32_t> g_destinationTextureLayout{0};
    std::atomic<std::uint32_t> g_destinationTextureFlags{0};
    std::atomic<std::int32_t> g_destinationTextureInitialState{0};

    D3D12_RESOURCE_DESC MakeDestinationDescription()
    {
        D3D12_RESOURCE_DESC description{};
        description.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;
        description.Alignment = 0;
        description.Width = kDestinationWidth;
        description.Height = kDestinationHeight;
        description.DepthOrArraySize = kDestinationDepthOrArraySize;
        description.MipLevels = kDestinationMipLevels;
        description.Format = kDestinationFormat;
        description.SampleDesc.Count = 1;
        description.SampleDesc.Quality = 0;
        description.Layout = D3D12_TEXTURE_LAYOUT_UNKNOWN;
        description.Flags = D3D12_RESOURCE_FLAG_NONE;
        return description;
    }

    bool MatchesDestinationSpecification(
        const D3D12_RESOURCE_DESC& description)
    {
        return
            description.Dimension == D3D12_RESOURCE_DIMENSION_TEXTURE2D
            && description.Width == kDestinationWidth
            && description.Height == kDestinationHeight
            && description.DepthOrArraySize == kDestinationDepthOrArraySize
            && description.MipLevels == kDestinationMipLevels
            && description.Format == kDestinationFormat
            && description.SampleDesc.Count == 1
            && description.SampleDesc.Quality == 0
            && description.Layout == D3D12_TEXTURE_LAYOUT_UNKNOWN
            && description.Flags == D3D12_RESOURCE_FLAG_NONE;
    }

    void StoreDescription(const D3D12_RESOURCE_DESC& description)
    {
        g_destinationTextureDimension.store(
            static_cast<std::int32_t>(description.Dimension),
            std::memory_order_relaxed);
        g_destinationTextureWidth.store(
            description.Width,
            std::memory_order_relaxed);
        g_destinationTextureHeight.store(
            description.Height,
            std::memory_order_relaxed);
        g_destinationTextureDepthOrArraySize.store(
            description.DepthOrArraySize,
            std::memory_order_relaxed);
        g_destinationTextureMipLevels.store(
            description.MipLevels,
            std::memory_order_relaxed);
        g_destinationTextureFormat.store(
            static_cast<std::int32_t>(description.Format),
            std::memory_order_relaxed);
        g_destinationTextureSampleCount.store(
            description.SampleDesc.Count,
            std::memory_order_relaxed);
        g_destinationTextureSampleQuality.store(
            description.SampleDesc.Quality,
            std::memory_order_relaxed);
        g_destinationTextureLayout.store(
            static_cast<std::int32_t>(description.Layout),
            std::memory_order_relaxed);
        g_destinationTextureFlags.store(
            static_cast<std::uint32_t>(description.Flags),
            std::memory_order_relaxed);
    }

    void ClearDescription()
    {
        g_destinationTextureDimension.store(0, std::memory_order_relaxed);
        g_destinationTextureWidth.store(0, std::memory_order_relaxed);
        g_destinationTextureHeight.store(0, std::memory_order_relaxed);
        g_destinationTextureDepthOrArraySize.store(0, std::memory_order_relaxed);
        g_destinationTextureMipLevels.store(0, std::memory_order_relaxed);
        g_destinationTextureFormat.store(0, std::memory_order_relaxed);
        g_destinationTextureSampleCount.store(0, std::memory_order_relaxed);
        g_destinationTextureSampleQuality.store(0, std::memory_order_relaxed);
        g_destinationTextureLayout.store(0, std::memory_order_relaxed);
        g_destinationTextureFlags.store(0, std::memory_order_relaxed);
    }

    void LogCreationFailure(HRESULT result)
    {
        wchar_t message[128]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Destination texture creation failed: "
            L"HRESULT=0x%08X\n",
            static_cast<unsigned int>(result));
        ::OutputDebugStringW(message);
    }
}

namespace DesktopMascotNative
{
    void ResetDestinationTextureDiagnostics()
    {
        ReleaseDestinationTexture();
        g_destinationTextureCreationResult.store(
            kDestinationTextureCreationNotAttempted,
            std::memory_order_relaxed);
        g_destinationTextureInitialState.store(0, std::memory_order_relaxed);
        ClearDescription();
    }

    void ReleaseDestinationTexture()
    {
        std::lock_guard lock(g_destinationTextureMutex);
        const bool hadTexture = g_destinationTexture != nullptr;
        g_destinationTextureAvailable.store(false, std::memory_order_release);
        g_destinationTexture.Reset();

        if (hadTexture)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Destination texture released.\n");
        }
    }

    void CreateDestinationTexture(ID3D12Device* device)
    {
        std::lock_guard lock(g_destinationTextureMutex);

        if (g_destinationTexture != nullptr)
        {
            const auto existingDescription = g_destinationTexture->GetDesc();
            if (MatchesDestinationSpecification(existingDescription))
            {
                g_destinationTextureAvailable.store(
                    true,
                    std::memory_order_release);
                return;
            }

            g_destinationTextureAvailable.store(
                false,
                std::memory_order_release);
            g_destinationTexture.Reset();
        }

        g_destinationTextureInitialState.store(
            static_cast<std::int32_t>(kDestinationInitialState),
            std::memory_order_relaxed);

        if (device == nullptr)
        {
            g_destinationTextureCreationResult.store(
                static_cast<std::int32_t>(E_POINTER),
                std::memory_order_relaxed);
            ClearDescription();
            LogCreationFailure(E_POINTER);
            return;
        }

        const D3D12_HEAP_PROPERTIES heapProperties{
            D3D12_HEAP_TYPE_DEFAULT,
            D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
            D3D12_MEMORY_POOL_UNKNOWN,
            1,
            1};
        const auto description = MakeDestinationDescription();

        ComPtr<ID3D12Resource> destinationTexture;
        const HRESULT result = device->CreateCommittedResource(
            &heapProperties,
            D3D12_HEAP_FLAG_NONE,
            &description,
            kDestinationInitialState,
            nullptr,
            IID_PPV_ARGS(destinationTexture.GetAddressOf()));
        g_destinationTextureCreationResult.store(
            static_cast<std::int32_t>(result),
            std::memory_order_relaxed);

        if (FAILED(result))
        {
            ClearDescription();
            LogCreationFailure(result);
            return;
        }

        const auto actualDescription = destinationTexture->GetDesc();
        StoreDescription(actualDescription);
        g_destinationTexture = std::move(destinationTexture);
        g_destinationTextureAvailable.store(true, std::memory_order_release);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Destination texture created.\n");
    }

    ID3D12Resource* GetDestinationTextureForRenderThread()
    {
        std::lock_guard lock(g_destinationTextureMutex);
        return g_destinationTexture.Get();
    }

    bool IsDestinationTextureAvailable()
    {
        return g_destinationTextureAvailable.load(std::memory_order_acquire);
    }

    std::int32_t GetDestinationTextureCreationResult()
    {
        return g_destinationTextureCreationResult.load(
            std::memory_order_relaxed);
    }

    std::int32_t GetDestinationTextureDimension()
    {
        return g_destinationTextureDimension.load(std::memory_order_relaxed);
    }

    std::uint64_t GetDestinationTextureWidth()
    {
        return g_destinationTextureWidth.load(std::memory_order_relaxed);
    }

    std::uint32_t GetDestinationTextureHeight()
    {
        return g_destinationTextureHeight.load(std::memory_order_relaxed);
    }

    std::uint32_t GetDestinationTextureDepthOrArraySize()
    {
        return g_destinationTextureDepthOrArraySize.load(
            std::memory_order_relaxed);
    }

    std::uint32_t GetDestinationTextureMipLevels()
    {
        return g_destinationTextureMipLevels.load(std::memory_order_relaxed);
    }

    std::int32_t GetDestinationTextureFormat()
    {
        return g_destinationTextureFormat.load(std::memory_order_relaxed);
    }

    std::uint32_t GetDestinationTextureSampleCount()
    {
        return g_destinationTextureSampleCount.load(std::memory_order_relaxed);
    }

    std::uint32_t GetDestinationTextureSampleQuality()
    {
        return g_destinationTextureSampleQuality.load(
            std::memory_order_relaxed);
    }

    std::int32_t GetDestinationTextureLayout()
    {
        return g_destinationTextureLayout.load(std::memory_order_relaxed);
    }

    std::uint32_t GetDestinationTextureFlags()
    {
        return g_destinationTextureFlags.load(std::memory_order_relaxed);
    }

    std::int32_t GetDestinationTextureInitialState()
    {
        return g_destinationTextureInitialState.load(std::memory_order_relaxed);
    }
}
