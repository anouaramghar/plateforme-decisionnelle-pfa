import { useCallback, useEffect, useRef, useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { CopilotChat } from "@copilotkit/react-core/v2";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";
import { CommandPalette } from "./CommandPalette";
import { Icon } from "../components/ui/Icon";
import { useAuth } from "../context/AuthContext";

export function AppShell() {
  const [cmdOpen, setCmdOpen] = useState(false);
  const [navOpen, setNavOpen] = useState(false);
  const [copilotOpen, setCopilotOpen] = useState(false);
  const lastActiveElementRef = useRef<HTMLElement | null>(null);
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const { copilotActive } = useAuth();
  const copilotTitleId = "eniad-copilot-title";

  const closeCopilot = useCallback(() => setCopilotOpen(false), []);

  // Route changes should never leave an off-canvas surface covering the page.
  useEffect(() => {
    setNavOpen(false);
    closeCopilot();
  }, [closeCopilot, pathname]);

  useEffect(() => {
    if (!copilotOpen) return;
    lastActiveElementRef.current =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;

    const mobile =
      typeof window.matchMedia === "function"
        ? window.matchMedia("(max-width: 639px)").matches
        : window.innerWidth < 640;
    if (mobile) document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = "";
      lastActiveElementRef.current?.focus();
      lastActiveElementRef.current = null;
    };
  }, [copilotOpen]);

  useEffect(() => {
    let gPressed = false;
    let gTimer: number | undefined;
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        setCmdOpen((open) => !open);
        return;
      }
      if ((e.metaKey || e.ctrlKey) && e.key === "j") {
        e.preventDefault();
        setCopilotOpen((open) => !open);
        return;
      }
      if (e.key === "Escape") {
        setCmdOpen(false);
        setNavOpen(false);
        closeCopilot();
        return;
      }

      const target = e.target as HTMLElement | null;
      const typing =
        !!target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable);
      if (typing || e.metaKey || e.ctrlKey || e.altKey) return;

      if (e.key === "g") {
        gPressed = true;
        window.clearTimeout(gTimer);
        gTimer = window.setTimeout(() => {
          gPressed = false;
        }, 800);
        return;
      }
      if (!gPressed) return;

      gPressed = false;
      window.clearTimeout(gTimer);
      const to = (
        {
          d: "/dashboard",
          e: "/students",
          a: "/alerts",
          p: "/predictions",
          r: "/reports",
        } as Record<string, string>
      )[e.key.toLowerCase()];
      if (to) {
        e.preventDefault();
        navigate(to);
      }
    };

    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
      window.clearTimeout(gTimer);
    };
  }, [closeCopilot, navigate]);

  return (
    <div className="vh-full flex" style={{ background: "var(--bg)" }}>
      {navOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/40 lg:hidden"
          aria-hidden="true"
          onClick={() => setNavOpen(false)}
        />
      )}

      <div
        id="primary-navigation"
        className={`fixed inset-y-0 left-0 z-50 h-full transition-transform duration-200 lg:static lg:z-auto lg:translate-x-0 ${
          navOpen ? "translate-x-0" : "-translate-x-full lg:translate-x-0"
        }`}
      >
        <Sidebar />
      </div>

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar
          onCommandOpen={() => setCmdOpen(true)}
          onCopilotOpen={() => setCopilotOpen((open) => !open)}
          onMenuOpen={() => setNavOpen(true)}
          navOpen={navOpen}
          copilotActive={copilotOpen}
        />
        <main className="scroll-thin flex-1 overflow-y-auto">
          <div className="app-content mx-auto max-w-[1480px] px-4 py-5 sm:px-6 lg:px-8 lg:py-7">
            <Outlet />
          </div>
        </main>
      </div>

      <CommandPalette open={cmdOpen} onClose={() => setCmdOpen(false)} />

      {copilotOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/30 sm:hidden"
          aria-hidden="true"
          onClick={closeCopilot}
        />
      )}
      {copilotOpen && (
        <aside
          className="copilot-panel fixed inset-y-0 right-0 z-50 flex w-full flex-col sm:w-[380px] sm:max-w-[42vw]"
          role="dialog"
          aria-modal="true"
          aria-labelledby={copilotTitleId}
          style={{
            background: "var(--surface)",
            borderLeft: "1px solid var(--border)",
            color: "var(--text)",
          }}
        >
          <div
            className="flex items-center justify-between p-4"
            style={{ borderBottom: "1px solid var(--border)" }}
          >
            <h2 id={copilotTitleId} className="text-lg font-semibold">
              ENIAD Copilot
            </h2>
            <button
              type="button"
              onClick={closeCopilot}
              className="btn btn-ghost flex h-11 w-11 items-center justify-center p-0"
              aria-label="Fermer le panneau Copilot"
            >
              <Icon name="x" size={18} />
            </button>
          </div>

          {copilotActive ? (
            <div className="min-h-0 flex-1 overflow-hidden">
              <CopilotChat
                agentId="default"
                labels={{
                  welcomeMessageText:
                    "Bonjour ! Interrogez les données étudiantes, les scores de risque et le DW en langage naturel.",
                  chatInputPlaceholder: "Posez une question sur les données…",
                }}
              />
            </div>
          ) : (
            <div className="flex flex-1 flex-col items-center justify-center p-6 text-center">
              <div className="mb-4" style={{ color: "var(--accent-500)" }}>
                <Icon name="alert" size={40} />
              </div>
              <p className="mb-1 font-medium">Copilot indisponible</p>
              <p className="text-sm" style={{ color: "var(--text-3)" }}>
                La session sécurisée n'a pas pu être établie. Le reste de la
                plateforme reste disponible.
              </p>
            </div>
          )}
        </aside>
      )}
    </div>
  );
}
