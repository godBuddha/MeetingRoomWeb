cmake_minimum_required(VERSION 3.20)
project(MeetingRoomLauncher VERSION 1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(FetchContent)

# ── ImGui ─────────────────────────────────────
FetchContent_Declare(
    imgui
    GIT_REPOSITORY https://github.com/ocornut/imgui.git
    GIT_TAG        v1.91.6
    GIT_SHALLOW    TRUE
)
FetchContent_MakeAvailable(imgui)

set(IMGUI_SOURCES
    ${imgui_SOURCE_DIR}/imgui.cpp
    ${imgui_SOURCE_DIR}/imgui_draw.cpp
    ${imgui_SOURCE_DIR}/imgui_tables.cpp
    ${imgui_SOURCE_DIR}/imgui_widgets.cpp
    ${imgui_SOURCE_DIR}/backends/imgui_impl_sdl2.cpp
    ${imgui_SOURCE_DIR}/backends/imgui_impl_opengl3.cpp
)

# ── SDL2 ──────────────────────────────────────
find_package(SDL2 QUIET)
if(NOT SDL2_FOUND)
    FetchContent_Declare(
        SDL2
        GIT_REPOSITORY https://github.com/libsdl-org/SDL.git
        GIT_TAG        release-2.30.9
        GIT_SHALLOW    TRUE
    )
    FetchContent_MakeAvailable(SDL2)
endif()

# ── Executable ────────────────────────────────
add_executable(MeetingRoomLauncher src/main.cpp ${IMGUI_SOURCES})

target_include_directories(MeetingRoomLauncher PRIVATE
    ${imgui_SOURCE_DIR}
    ${imgui_SOURCE_DIR}/backends
)

# ── Platform link ─────────────────────────────
if(WIN32)
    target_link_libraries(MeetingRoomLauncher PRIVATE SDL2::SDL2 SDL2::SDL2main opengl32)
    set_target_properties(MeetingRoomLauncher PROPERTIES WIN32_EXECUTABLE TRUE)
elseif(APPLE)
    find_package(OpenGL REQUIRED)
    target_link_libraries(MeetingRoomLauncher PRIVATE SDL2::SDL2 OpenGL::GL)
else()
    find_package(OpenGL REQUIRED)
    target_link_libraries(MeetingRoomLauncher PRIVATE SDL2::SDL2 OpenGL::GL dl pthread)
endif()
