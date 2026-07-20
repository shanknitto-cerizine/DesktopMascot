#pragma once

#include <cstdint>

struct ID3D12Device;
struct ID3D12Resource;

namespace DesktopMascotNative
{
    constexpr std::int32_t kDestinationTextureCreationNotAttempted =
        0x7FFFFFFF;

    void ResetDestinationTextureDiagnostics();
    void ReleaseDestinationTexture();
    void CreateDestinationTexture(ID3D12Device* device);
    // Borrowed pointer for a Unity render callback only. Do not AddRef, Release,
    // retain, or access it after the callback returns.
    ID3D12Resource* GetDestinationTextureForRenderThread();

    bool IsDestinationTextureAvailable();
    std::int32_t GetDestinationTextureCreationResult();
    std::int32_t GetDestinationTextureDimension();
    std::uint64_t GetDestinationTextureWidth();
    std::uint32_t GetDestinationTextureHeight();
    std::uint32_t GetDestinationTextureDepthOrArraySize();
    std::uint32_t GetDestinationTextureMipLevels();
    std::int32_t GetDestinationTextureFormat();
    std::uint32_t GetDestinationTextureSampleCount();
    std::uint32_t GetDestinationTextureSampleQuality();
    std::int32_t GetDestinationTextureLayout();
    std::uint32_t GetDestinationTextureFlags();
    std::int32_t GetDestinationTextureInitialState();
}
