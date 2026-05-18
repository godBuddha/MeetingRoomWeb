// MeetingRoomLauncher — Cross-Platform Dear ImGui Launcher
// Windows / macOS / Ubuntu
// Build: see build_*.sh / build_*.bat

#if defined(__APPLE__)
  #define GL_SILENCE_DEPRECATION
#endif

#include "imgui.h"
#include "imgui_impl_sdl2.h"
#include "imgui_impl_opengl3.h"
#include <SDL.h>
#if defined(__APPLE__)
  #include <OpenGL/gl3.h>
#else
  #include <GL/gl.h>
#endif

#include <string>
#include <vector>
#include <deque>
#include <thread>
#include <mutex>
#include <atomic>
#include <functional>
#include <cstdio>
#include <cstring>
#include <chrono>
#include <sstream>

#if defined(_WIN32)
  #define WIN32_LEAN_AND_MEAN
  #include <windows.h>
#else
  #include <unistd.h>
  #include <sys/wait.h>
#endif

// ─── Cross-platform process helpers ──────────────────────────────────────────
static std::string RunCommand(const std::string& cmd) {
    std::string result;
#if defined(_WIN32)
    FILE* pipe = _popen(cmd.c_str(), "r");
#else
    FILE* pipe = popen(cmd.c_str(), "r");
#endif
    if (!pipe) return "";
    char buf[256];
    while (fgets(buf, sizeof(buf), pipe)) result += buf;
#if defined(_WIN32)
    _pclose(pipe);
#else
    pclose(pipe);
#endif
    return result;
}

static void LaunchDetached(const std::string& cmd) {
#if defined(_WIN32)
    STARTUPINFOA si{}; si.cb = sizeof(si);
    PROCESS_INFORMATION pi{};
    std::string c = "cmd.exe /C " + cmd;
    CreateProcessA(nullptr, c.data(), nullptr, nullptr,
                   FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi);
    CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
#else
    // fork + exec, detach from parent
    if (fork() == 0) {
        setsid();
        execl("/bin/sh", "sh", "-c", cmd.c_str(), nullptr);
        _exit(0);
    }
#endif
}

// ─── Platform commands ────────────────────────────────────────────────────────
#if defined(_WIN32)
  // Project path on Windows (adjust if needed)
  static const std::string PROJECT_PATH    = "C:\\MeetingRoomWeb";
  static const std::string DOTNET_CMD      = "dotnet run --project \"" + PROJECT_PATH + "\\MeetingRoomWeb.csproj\"";
  static const std::string DOCKER_START    = "start \"\" \"C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe\"";
  static const std::string PG_START        = "docker start meeting-postgres";
  static const std::string PG_STOP         = "docker stop meeting-postgres";
  static const std::string TS_FUNNEL       = "tailscale funnel --bg 5038";
  static const std::string TS_FUNNEL_OFF   = "tailscale funnel --off";
  static const std::string DOCKER_CHECK    = "docker info 2>&1 | findstr /C:\"Server Version\"";
  static const std::string PG_CHECK        = "docker inspect -f \"{{.State.Running}}\" meeting-postgres 2>&1";
  static const std::string TS_CHECK        = "tailscale funnel status 2>&1";
  static const std::string TS_DAEMON_CHECK = "tailscale status 2>&1";
  static const std::string TS_DAEMON_START = "powershell -Command \"Start-Service Tailscale\"";
#elif defined(__APPLE__)
  static const std::string PROJECT_PATH    = "/Volumes/PortableSSD/Congviec/MeetingRoomWeb";
  static const std::string DOTNET_CMD      = "cd \"" + PROJECT_PATH + "\" && dotnet run --project MeetingRoomWeb.csproj";
  static const std::string DOCKER_START    = "open -a Docker";
  static const std::string PG_START        = "docker start meeting-postgres";
  static const std::string PG_STOP         = "docker stop meeting-postgres";
  static const std::string TS_FUNNEL       = "tailscale funnel --bg 5038";
  static const std::string TS_FUNNEL_OFF   = "tailscale funnel --off";
  static const std::string DOCKER_CHECK    = "docker info 2>/dev/null | grep 'Server Version'";
  static const std::string PG_CHECK        = "docker inspect -f '{{.State.Running}}' meeting-postgres 2>/dev/null";
  static const std::string TS_CHECK        = "tailscale funnel status 2>/dev/null";
  static const std::string TS_DAEMON_CHECK = "tailscale status 2>/dev/null";
  static const std::string TS_DAEMON_START = "pkill tailscaled 2>/dev/null; sleep 1; sudo /opt/homebrew/bin/tailscaled --state=/Library/Tailscale/tailscaled.state --statedir=/Library/Tailscale &";

#else // Linux / Ubuntu
  static const std::string PROJECT_PATH    = "/opt/MeetingRoomWeb";
  static const std::string DOTNET_CMD      = "cd \"" + PROJECT_PATH + "\" && dotnet run --project MeetingRoomWeb.csproj";
  static const std::string DOCKER_START    = "sudo systemctl start docker";
  static const std::string PG_START        = "docker start meeting-postgres";
  static const std::string PG_STOP         = "docker stop meeting-postgres";
  static const std::string TS_FUNNEL       = "tailscale funnel --bg 5038";
  static const std::string TS_FUNNEL_OFF   = "tailscale funnel --off";
  static const std::string DOCKER_CHECK    = "docker info 2>/dev/null | grep 'Server Version'";
  static const std::string PG_CHECK        = "docker inspect -f '{{.State.Running}}' meeting-postgres 2>/dev/null";
  static const std::string TS_CHECK        = "tailscale funnel status 2>/dev/null";
  static const std::string TS_DAEMON_CHECK = "tailscale status 2>/dev/null";
  static const std::string TS_DAEMON_START = "sudo tailscaled --state=/var/lib/tailscale/tailscaled.state &";
#endif

// ─── Service model ────────────────────────────────────────────────────────────
enum class ServiceStatus { Unknown, Stopped, Starting, Running, Error };

static const char* StatusLabel(ServiceStatus s) {
    switch (s) {
        case ServiceStatus::Running:  return "Running";
        case ServiceStatus::Starting: return "Starting...";
        case ServiceStatus::Stopped:  return "Stopped";
        case ServiceStatus::Error:    return "Error";
        default:                      return "Checking...";
    }
}
static ImVec4 StatusColor(ServiceStatus s) {
    switch (s) {
        case ServiceStatus::Running:  return {0.2f, 0.9f, 0.4f, 1.0f};
        case ServiceStatus::Starting: return {1.0f, 0.8f, 0.1f, 1.0f};
        case ServiceStatus::Error:    return {1.0f, 0.3f, 0.3f, 1.0f};
        default:                      return {0.6f, 0.6f, 0.6f, 1.0f};
    }
}

struct Service {
    const char*                  icon;
    const char*                  name;
    ServiceStatus                status = ServiceStatus::Unknown;
    std::function<bool()>        checkFn;
    std::function<void()>        startFn;
    std::function<void()>        stopFn;
};

static std::mutex g_svcMx;  // protect status writes from threads

static void SetStatus(Service& svc, ServiceStatus s) {
    std::lock_guard<std::mutex> lk(g_svcMx);
    svc.status = s;
}
static ServiceStatus GetStatus(const Service& svc) {
    std::lock_guard<std::mutex> lk(g_svcMx);
    return svc.status;
}

// ─── Log ──────────────────────────────────────────────────────────────────────
static std::deque<std::string> g_log;
static std::mutex              g_logMx;

static void Log(const std::string& msg) {
    auto now = std::chrono::system_clock::now();
    auto t   = std::chrono::system_clock::to_time_t(now);
    char ts[16]; std::strftime(ts, sizeof(ts), "%H:%M:%S", std::localtime(&t));
    std::lock_guard<std::mutex> lk(g_logMx);
    g_log.push_back(std::string("[") + ts + "] " + msg);
    if (g_log.size() > 200) g_log.pop_front();
}

// ─── App process handle ───────────────────────────────────────────────────────
static std::atomic<bool> g_appRunning{false};
static std::thread       g_appThread;

// ─── Tailscale auth state ─────────────────────────────────────────────────────
static std::atomic<bool> g_tsLoggedOut{false};  // true = token expired / logged out
static std::string       g_tsLoginUrl;
static std::mutex        g_tsLoginUrlMx;

// ─── Service definitions ──────────────────────────────────────────────────────
static Service g_services[5];

static void InitServices() {
    // Docker
    g_services[0] = {
        "   [Docker]", "Docker Desktop",
        {}, // status
        []() -> bool {
            auto out = RunCommand(DOCKER_CHECK);
            return out.find("Server") != std::string::npos ||
                   out.find("Version") != std::string::npos;
        },
        []() {
                Log("Starting Docker Desktop...");
                SetStatus(g_services[0], ServiceStatus::Starting);
                LaunchDetached(DOCKER_START);
                std::thread([]() {
                    for (int i = 0; i < 30; i++) {
                        std::this_thread::sleep_for(std::chrono::seconds(2));
                        auto out = RunCommand(DOCKER_CHECK);
                        if (out.find("Version") != std::string::npos ||
                            out.find("Server") != std::string::npos) {
                            SetStatus(g_services[0], ServiceStatus::Running);
                            Log("[+] Docker Desktop is running.");
                            return;
                        }
                    }
                    SetStatus(g_services[0], ServiceStatus::Error);
                    Log("[!] Docker Desktop timeout.");
                }).detach();
            },
        []() { Log("Docker: manual stop via Docker Desktop app."); }
    };

    // PostgreSQL
    g_services[1] = {
        "   [Postgres]", "PostgreSQL (Docker)",
        {},
        []() -> bool {
            auto out = RunCommand(PG_CHECK);
            return out.find("true") != std::string::npos;
        },
        []() {
                Log("Starting meeting-postgres...");
                SetStatus(g_services[1], ServiceStatus::Starting);
                std::thread([]() {
                    RunCommand(PG_START);
                    std::this_thread::sleep_for(std::chrono::seconds(2));
                    auto out = RunCommand(PG_CHECK);
                    if (out.find("true") != std::string::npos) {
                        SetStatus(g_services[1], ServiceStatus::Running);
                        Log("[+] PostgreSQL is running.");
                    } else {
                        SetStatus(g_services[1], ServiceStatus::Error);
                        Log("[!] PostgreSQL failed to start.");
                    }
                }).detach();
            },
            []() {
                Log("Stopping PostgreSQL...");
                std::thread([]() {
                    RunCommand(PG_STOP);
                    SetStatus(g_services[1], ServiceStatus::Stopped);
                    Log("[=] PostgreSQL stopped.");
                }).detach();
            }
    };

    // Tailscale Daemon
    g_services[2] = {
        "   [TS Daemon]", "Tailscale (Daemon)",
        {},
        []() -> bool {
            auto out = RunCommand(TS_DAEMON_CHECK);
            // Failed = daemon not running
            if (out.find("failed to connect") != std::string::npos) {
                g_tsLoggedOut = false;
                return false;
            }
            if (out.find("is Tailscale running") != std::string::npos) {
                g_tsLoggedOut = false;
                return false;
            }
            // Logged out = token expired
            if (out.find("Logged out") != std::string::npos ||
                out.find("logged out") != std::string::npos) {
                g_tsLoggedOut = true;
                Log("[!] Tailscale: session expired. Click 'Login' to re-authenticate.");
                return false;
            }
            g_tsLoggedOut = false;
            // Has a self IP or logged-in node = daemon up
            return out.find(".") != std::string::npos && out.size() > 10;
        },
        []() {
                Log("Starting Tailscale daemon/app...");
                SetStatus(g_services[2], ServiceStatus::Starting);
                LaunchDetached(TS_DAEMON_START);
                std::thread([]() {
                    for (int i = 0; i < 20; i++) {
                        std::this_thread::sleep_for(std::chrono::seconds(2));
                        auto out = RunCommand(TS_DAEMON_CHECK);
                        if (out.find("failed to connect") == std::string::npos &&
                            out.find("is Tailscale running") == std::string::npos &&
                            out.size() > 10) {
                            SetStatus(g_services[2], ServiceStatus::Running);
                            Log("[+] Tailscale daemon is running.");
                            return;
                        }
                    }
                    SetStatus(g_services[2], ServiceStatus::Error);
                    Log("[!] Tailscale daemon did not start in time. Open Tailscale app manually.");
                }).detach();
            },
            []() {
                Log("[!] To stop Tailscale: quit the Tailscale app from the menu bar.");
            }
    };

    // Tailscale Funnel
    g_services[3] = {
        "   [Funnel]", "Tailscale Funnel",
        {},
        []() -> bool {
            auto out = RunCommand(TS_CHECK);
            return out.find("5038") != std::string::npos ||
                   out.find("proxy") != std::string::npos ||
                   out.find("Funnel on") != std::string::npos;
        },
        []() {
                // Ensure daemon is up first
                auto daemonOut = RunCommand(TS_DAEMON_CHECK);
                if (daemonOut.find("failed to connect") != std::string::npos ||
                    daemonOut.find("is Tailscale running") != std::string::npos) {
                    Log("[!] Tailscale daemon not running. Start 'Tailscale (Daemon)' first.");
                    SetStatus(g_services[3], ServiceStatus::Error);
                    return;
                }
                Log("Enabling Tailscale Funnel on port 5038...");
                SetStatus(g_services[3], ServiceStatus::Starting);
                std::thread([]() {
                    RunCommand(TS_FUNNEL);
                    std::this_thread::sleep_for(std::chrono::seconds(1));
                    auto out = RunCommand(TS_CHECK);
                    if (out.find("5038") != std::string::npos ||
                        out.find("proxy") != std::string::npos ||
                        out.find("Funnel") != std::string::npos) {
                        SetStatus(g_services[3], ServiceStatus::Running);
                        Log("[+] Tailscale Funnel active: https://2026.tailba3e29.ts.net");
                    } else {
                        SetStatus(g_services[3], ServiceStatus::Error);
                        Log("[!] Tailscale Funnel failed.");
                    }
                }).detach();
            },
            []() {
                Log("Disabling Tailscale Funnel...");
                std::thread([]() {
                    RunCommand(TS_FUNNEL_OFF);
                    SetStatus(g_services[3], ServiceStatus::Stopped);
                    Log("[=] Tailscale Funnel disabled.");
                }).detach();
            }
    };

    // MeetingRoom App
    g_services[4] = {
        "   [App]", "MeetingRoom Web (.NET)",
        {},
        []() -> bool { return g_appRunning.load(); },
        []() {
                if (g_appRunning.load()) { Log("[!] App already running."); return; }
                Log("Starting MeetingRoom Web...");
                SetStatus(g_services[4], ServiceStatus::Starting);
                g_appThread = std::thread([]() {
                    g_appRunning = true;
                    SetStatus(g_services[4], ServiceStatus::Running);
                    Log("[+] dotnet run started. URL: http://localhost:5038");
                    RunCommand(DOTNET_CMD + " 2>&1");
                    g_appRunning = false;
                    SetStatus(g_services[4], ServiceStatus::Stopped);
                    Log("[=] dotnet run exited.");
                });
                g_appThread.detach();
            },
            []() {
#if defined(_WIN32)
                RunCommand("taskkill /F /IM dotnet.exe /T 2>nul");
#else
                RunCommand("pkill -f 'dotnet run' 2>/dev/null; pkill -f MeetingRoomWeb 2>/dev/null");
#endif
                g_appRunning = false;
                SetStatus(g_services[4], ServiceStatus::Stopped);
                Log("[=] MeetingRoom Web stopped.");
            }
    };
}

// ─── Status polling (background thread) ──────────────────────────────────────
static std::atomic<bool> g_pollRunning{true};

static void PollThread() {
    while (g_pollRunning) {
        for (auto& s : g_services) {
            {
                std::lock_guard<std::mutex> lk(g_svcMx);
                if (s.status == ServiceStatus::Starting) continue;
            }
            bool ok = s.checkFn();
            SetStatus(s, ok ? ServiceStatus::Running : ServiceStatus::Stopped);
        }
        std::this_thread::sleep_for(std::chrono::seconds(3));
    }
}

// ─── Main ─────────────────────────────────────────────────────────────────────
int main(int, char**) {
    InitServices();

    // SDL init
    if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_TIMER) != 0) return -1;

    SDL_GL_SetAttribute(SDL_GL_CONTEXT_FLAGS, 0);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 3);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 2);
    SDL_GL_SetAttribute(SDL_GL_DOUBLEBUFFER, 1);

    SDL_Window* window = SDL_CreateWindow(
        "MeetingRoom Launcher",
        SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
        740, 560,
        SDL_WINDOW_OPENGL | SDL_WINDOW_RESIZABLE | SDL_WINDOW_ALLOW_HIGHDPI
    );
    SDL_GLContext gl_ctx = SDL_GL_CreateContext(window);
    SDL_GL_MakeCurrent(window, gl_ctx);
    SDL_GL_SetSwapInterval(1);

    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.IniFilename = nullptr;

    // Style — dark mode
    ImGui::StyleColorsDark();
    ImGuiStyle& style = ImGui::GetStyle();
    style.WindowRounding   = 8.0f;
    style.FrameRounding    = 6.0f;
    style.ItemSpacing      = {10, 8};
    style.FramePadding     = {10, 5};
    style.Colors[ImGuiCol_WindowBg]       = {0.10f, 0.10f, 0.13f, 1.0f};
    style.Colors[ImGuiCol_Header]         = {0.20f, 0.20f, 0.28f, 1.0f};
    style.Colors[ImGuiCol_ButtonHovered]  = {0.28f, 0.56f, 1.00f, 1.0f};
    style.Colors[ImGuiCol_Button]         = {0.18f, 0.40f, 0.80f, 1.0f};
    style.Colors[ImGuiCol_ButtonActive]   = {0.20f, 0.48f, 0.90f, 1.0f};

    ImGui_ImplSDL2_InitForOpenGL(window, gl_ctx);
    ImGui_ImplOpenGL3_Init("#version 150");

    // Start polling
    std::thread poller(PollThread);

    Log("MeetingRoom Launcher started.");

    bool quit = false;
    while (!quit) {
        SDL_Event e;
        while (SDL_PollEvent(&e)) {
            ImGui_ImplSDL2_ProcessEvent(&e);
            if (e.type == SDL_QUIT) quit = true;
        }

        ImGui_ImplOpenGL3_NewFrame();
        ImGui_ImplSDL2_NewFrame();
        ImGui::NewFrame();

        // Fullscreen window
        ImGui::SetNextWindowPos({0, 0});
        ImGui::SetNextWindowSize(io.DisplaySize);
        ImGui::Begin("##main", nullptr,
            ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
            ImGuiWindowFlags_NoMove     | ImGuiWindowFlags_NoScrollbar);

        // ── Header ──────────────────────────────────────────────────────────
        ImGui::SetWindowFontScale(1.4f);
        ImGui::TextColored({0.4f, 0.8f, 1.0f, 1.0f}, "  MeetingRoom Launcher");
        ImGui::SetWindowFontScale(1.0f);
        ImGui::SameLine();
        ImGui::TextDisabled("  v1.0 | https://2026-1.tailba3e29.ts.net");
        ImGui::Separator();
        ImGui::Spacing();

        // ── Tailscale re-login banner ────────────────────────────────────────
        if (g_tsLoggedOut.load()) {
            float bannerW = ImGui::GetContentRegionAvail().x;
            ImGui::PushStyleColor(ImGuiCol_ChildBg, {0.55f, 0.10f, 0.10f, 1.0f});
            ImGui::BeginChild("ts_warn", {bannerW, 44}, ImGuiChildFlags_None);
            ImGui::SetCursorPos({10, 10});
            ImGui::TextColored({1.0f, 0.9f, 0.2f, 1.0f},
                "  ⚠  Tailscale session expired! Re-authentication required.");
            ImGui::SameLine();
            ImGui::SetCursorPosY(6);
            ImGui::PushStyleColor(ImGuiCol_Button,        {0.85f, 0.85f, 0.10f, 1.0f});
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, {1.00f, 1.00f, 0.20f, 1.0f});
            ImGui::PushStyleColor(ImGuiCol_ButtonActive,  {0.75f, 0.75f, 0.05f, 1.0f});
            if (ImGui::Button("  Login  ", {80, 28})) {
                std::thread([]() {
                    Log("Opening Tailscale login...");
                    // Run tailscale login and capture the URL
                    auto url = RunCommand("tailscale login --timeout=5s 2>&1 | grep 'https://login' | head -1");
                    if (!url.empty()) {
                        std::lock_guard<std::mutex> lk(g_tsLoginUrlMx);
                        g_tsLoginUrl = url;
                        // Open URL in default browser
                        url.erase(url.find_last_not_of(" \n\r\t") + 1);
#if defined(__APPLE__)
                        LaunchDetached("open '" + url + "'");
#elif defined(_WIN32)
                        LaunchDetached("start \"\" \"" + url + "\"");
#else
                        LaunchDetached("xdg-open '" + url + "'");
#endif
                        Log("[+] Browser opened: " + url);
                    } else {
                        Log("[!] Could not get login URL. Run: tailscale login");
                    }
                }).detach();
            }
            ImGui::PopStyleColor(3);
            ImGui::EndChild();
            ImGui::PopStyleColor();
            ImGui::Spacing();
        }

        // ── Services table ───────────────────────────────────────────────────
        if (ImGui::BeginTable("services", 4,
            ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_RowBg | ImGuiTableFlags_PadOuterX,
            {0, 190}))
        {
            ImGui::TableSetupColumn("Service",  ImGuiTableColumnFlags_WidthStretch);
            ImGui::TableSetupColumn("Status",   ImGuiTableColumnFlags_WidthFixed, 110);
            ImGui::TableSetupColumn("Start",    ImGuiTableColumnFlags_WidthFixed, 80);
            ImGui::TableSetupColumn("Stop",     ImGuiTableColumnFlags_WidthFixed, 80);
            ImGui::TableHeadersRow();

            for (auto& svc : g_services) {
                ImGui::TableNextRow(ImGuiTableRowFlags_None, 38);

                // Name
                ImGui::TableSetColumnIndex(0);
                ImGui::SetCursorPosY(ImGui::GetCursorPosY() + 8);
                ImGui::Text("%s  %s", svc.icon, svc.name);

                // Status
                ImGui::TableSetColumnIndex(1);
                ImGui::SetCursorPosY(ImGui::GetCursorPosY() + 8);
                auto st = GetStatus(svc);
                ImGui::TextColored(StatusColor(st), "  %s", StatusLabel(st));

                // Start
                ImGui::TableSetColumnIndex(2);
                ImGui::SetCursorPosY(ImGui::GetCursorPosY() + 5);
                ImGui::PushID(svc.name);
                bool canStart = (st == ServiceStatus::Stopped || st == ServiceStatus::Unknown || st == ServiceStatus::Error);
                if (!canStart) ImGui::BeginDisabled();
                if (ImGui::Button("Start", {68, 28})) {
                    std::thread([&svc]() { svc.startFn(); }).detach();
                }
                if (!canStart) ImGui::EndDisabled();

                // Stop
                ImGui::TableSetColumnIndex(3);
                ImGui::SetCursorPosY(ImGui::GetCursorPosY() + 5);
                ImGui::PushStyleColor(ImGuiCol_Button,        {0.7f, 0.15f, 0.15f, 1.0f});
                ImGui::PushStyleColor(ImGuiCol_ButtonHovered, {0.9f, 0.25f, 0.25f, 1.0f});
                ImGui::PushStyleColor(ImGuiCol_ButtonActive,  {0.8f, 0.10f, 0.10f, 1.0f});
                bool canStop = (st == ServiceStatus::Running);
                if (!canStop) ImGui::BeginDisabled();
                if (ImGui::Button("Stop", {68, 28})) {
                    std::thread([&svc]() { svc.stopFn(); }).detach();
                }
                if (!canStop) ImGui::EndDisabled();
                ImGui::PopStyleColor(3);
                ImGui::PopID();
            }
            ImGui::EndTable();
        }

        ImGui::Spacing();

        // ── Start All / Stop All ─────────────────────────────────────────────
        float bw = (ImGui::GetContentRegionAvail().x - 10) / 2;
        ImGui::PushStyleColor(ImGuiCol_Button,        {0.12f, 0.60f, 0.20f, 1.0f});
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, {0.18f, 0.80f, 0.30f, 1.0f});
        ImGui::PushStyleColor(ImGuiCol_ButtonActive,  {0.10f, 0.50f, 0.18f, 1.0f});
        if (ImGui::Button("  Start All", {bw, 38})) {
            // Start sequentially: Docker → Postgres → Tailscale Daemon → Funnel → App
            // Each step waits for previous to be Running before proceeding
            std::thread([]() {
                // Step 0: Docker
                g_services[0].startFn();
                for (int i = 0; i < 30 && GetStatus(g_services[0]) != ServiceStatus::Running; i++)
                    std::this_thread::sleep_for(std::chrono::seconds(2));
                // Step 1: Postgres
                g_services[1].startFn();
                for (int i = 0; i < 15 && GetStatus(g_services[1]) != ServiceStatus::Running; i++)
                    std::this_thread::sleep_for(std::chrono::seconds(1));
                // Step 2: Tailscale Daemon
                g_services[2].startFn();
                for (int i = 0; i < 20 && GetStatus(g_services[2]) != ServiceStatus::Running; i++)
                    std::this_thread::sleep_for(std::chrono::seconds(2));
                // Step 3: Tailscale Funnel
                g_services[3].startFn();
                std::this_thread::sleep_for(std::chrono::seconds(2));
                // Step 4: MeetingRoom Web
                g_services[4].startFn();
            }).detach();
        }
        ImGui::PopStyleColor(3);

        ImGui::SameLine(0, 10);

        ImGui::PushStyleColor(ImGuiCol_Button,        {0.65f, 0.12f, 0.12f, 1.0f});
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, {0.85f, 0.20f, 0.20f, 1.0f});
        ImGui::PushStyleColor(ImGuiCol_ButtonActive,  {0.55f, 0.10f, 0.10f, 1.0f});
        if (ImGui::Button("  Stop All",  {bw, 38})) {
            for (auto& svc : g_services) {
                std::thread([&svc]() { svc.stopFn(); }).detach();
            }
        }
        ImGui::PopStyleColor(3);

        ImGui::Spacing();
        ImGui::Separator();
        ImGui::Spacing();

        // ── Log panel ────────────────────────────────────────────────────────
        ImGui::TextDisabled("  Log");
        ImGui::SameLine(ImGui::GetContentRegionAvail().x - 60);
        if (ImGui::SmallButton("Clear")) {
            std::lock_guard<std::mutex> lk(g_logMx);
            g_log.clear();
        }

        ImGui::BeginChild("log", {0, 0}, ImGuiChildFlags_Border,
            ImGuiWindowFlags_HorizontalScrollbar);
        {
            std::lock_guard<std::mutex> lk(g_logMx);
            for (auto& line : g_log) {
                if (line.find("[+]") != std::string::npos)
                    ImGui::TextColored({0.3f, 0.9f, 0.4f, 1.0f}, "%s", line.c_str());
                else if (line.find("[!]") != std::string::npos)
                    ImGui::TextColored({1.0f, 0.6f, 0.1f, 1.0f}, "%s", line.c_str());
                else if (line.find("[=]") != std::string::npos)
                    ImGui::TextColored({0.7f, 0.7f, 0.7f, 1.0f}, "%s", line.c_str());
                else
                    ImGui::TextUnformatted(line.c_str());
            }
            if (ImGui::GetScrollY() >= ImGui::GetScrollMaxY())
                ImGui::SetScrollHereY(1.0f);
        }
        ImGui::EndChild();
        ImGui::End();

        // Render
        glViewport(0, 0, (int)io.DisplaySize.x, (int)io.DisplaySize.y);
        glClearColor(0.10f, 0.10f, 0.13f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);
        ImGui::Render();
        ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());
        SDL_GL_SwapWindow(window);
    }

    // Cleanup
    g_pollRunning = false;
    poller.join();

    ImGui_ImplOpenGL3_Shutdown();
    ImGui_ImplSDL2_Shutdown();
    ImGui::DestroyContext();
    SDL_GL_DeleteContext(gl_ctx);
    SDL_DestroyWindow(window);
    SDL_Quit();
    return 0;
}
