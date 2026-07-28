cmake_minimum_required(VERSION 3.21)

project(DesktopMascotNative LANGUAGES CXX)

add_library(DesktopMascotNative SHARED
    src/AnimatedWindowRegionDiagnostics.cpp
    src/CompositionAlphaMaskDiagnostics.cpp
    src/CompositionClickThroughDiagnostics.cpp
    src/CompositionDiagnostics.cpp
    src/CompositionPixelHitTestDiagnostics.cpp
    src/CompositionWindowRegionDiagnostics.cpp
    src/CompositionPresentDiagnostics.cpp
    src/CompositionWindowPositionDiagnostics.cpp
    src/ContinuousCompositionDiagnostics.cpp
    src/CopyResourceDiagnostics.cpp
    src/DestinationTexture.cpp
    src/NativeMascotContextMenu.cpp
    src/NativeTrayIcon.cpp
    src/NativeMascotWindowDrag.cpp
    src/Plugin.cpp
    src/ReadbackDiagnostics.cpp
    src/SingleInstanceCoordinator.cpp
    src/StaticComplexSilhouetteDiagnostics.cpp
)

target_link_libraries(DesktopMascotNative
    PRIVATE
        dcomp
        dxgi
        gdi32
        ole32
        shell32
        user32
)

target_compile_features(DesktopMascotNative PRIVATE cxx_std_20)

target_include_directories(DesktopMascotNative
    PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/include"
        "${CMAKE_CURRENT_SOURCE_DIR}/unity"
)

target_compile_definitions(DesktopMascotNative
    PRIVATE
        WIN32_LEAN_AND_MEAN
        NOMINMAX
)

if(MSVC)
    target_compile_options(DesktopMascotNative
        PRIVATE
            /W4
            /permissive-
            /Zc:__cplusplus
            /utf-8
    )
endif()

set_target_properties(DesktopMascotNative PROPERTIES
    CXX_EXTENSIONS OFF
    MSVC_RUNTIME_LIBRARY "MultiThreadedDLL"
    OUTPUT_NAME "DesktopMascotNative"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../Assets/Plugins/x86_64"
)
