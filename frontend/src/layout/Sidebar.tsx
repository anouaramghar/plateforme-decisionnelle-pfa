import { useRef, useState, useEffect } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Icon, type IconName } from "../components/ui/Icon";
import { Avatar } from "../components/ui/Avatar";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useFiliere } from "../context/FiliereContext";
import { api } from "../services/api";
import { useUnresolvedAlertCount } from "../services/useUnresolvedAlertCount";

const FILIERES = ["TOUS", "EPSI", "IA", "ROC", "IRSI", "GINF"];

interface NavEntry {
  to: string;
  label: string;
  icon: IconName;
  badge?: number;
  badgeTone?: "bad" | "warn" | "neutral";
}

interface EtudiantRow {
  id: number;
}

const SECONDARY: NavEntry[] = [
  { to: "/admin", label: "Administration", icon: "database" },
  { to: "/settings", label: "Paramètres", icon: "settings" },
];

export function Sidebar() {
  const { sidebarCollapsed, toggleSidebar } = useTheme();
  const { user, token, logout } = useAuth();
  const navigate = useNavigate();
  const { filiere, setFiliere } = useFiliere();
  const W = sidebarCollapsed ? 64 : 232;
  const mayAccessAlerts =
    user?.role === "Admin" || user?.role === "Responsable";

  const [dropOpen, setDropOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const dropRef = useRef<HTMLDivElement>(null);
  const userRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (dropRef.current && !dropRef.current.contains(e.target as Node)) {
        setDropOpen(false);
      }
      if (userRef.current && !userRef.current.contains(e.target as Node)) {
        setUserMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setDropOpen(false);
        setUserMenuOpen(false);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  // Live badge counts. Gated on `token` so we don't fire unauthenticated calls
  // before login. Alert count is shared with the topbar (one poll, not two).
  const { data: alertesData } = useUnresolvedAlertCount(
    !!token && mayAccessAlerts,
  );
  const { data: etudiantsCount } = useQuery({
    queryKey: ["sidebar", "etudiants-count"],
    queryFn: async () => {
      const res = await api.get<EtudiantRow[]>("/etudiants/with-stats");
      return res.data.length;
    },
    enabled: !!token,
    refetchInterval: 5 * 60_000,
    staleTime: 4 * 60_000,
  });

  const isEns = user?.role === "Enseignant";
  const isResp = user?.role === "Responsable";
  const isAdmin = user?.role === "Admin";

  // Each role's own workspace leads the list; institution tools follow and are
  // hidden when the role can't use them.
  const PRIMARY: NavEntry[] = [
    ...(isEns
      ? [{ to: "/enseignant", label: "Mon espace", icon: "doc" as IconName }]
      : []),
    ...(isResp
      ? [
          {
            to: "/responsable",
            label: "Mon espace",
            icon: "graduation" as IconName,
          },
        ]
      : []),
    ...(isAdmin || isResp
      ? [
          {
            to: "/dashboard",
            label: "Tableau de bord",
            icon: "dashboard" as IconName,
          },
        ]
      : []),
    {
      to: "/students",
      label: "Étudiants",
      icon: "students",
      badge: etudiantsCount,
    },
    ...(mayAccessAlerts
      ? [
          {
            to: "/alerts",
            label: "Alertes",
            icon: "bell" as IconName,
            badge: alertesData,
            badgeTone: "bad" as const,
          },
        ]
      : []),
    ...(mayAccessAlerts
      ? [{ to: "/cases", label: "Interventions", icon: "bookmark" as IconName }]
      : []),
    ...(!isEns
      ? [
          {
            to: "/predictions",
            label: "Prédictions ML",
            icon: "brain" as IconName,
          },
        ]
      : []),
    { to: "/reports", label: "Rapports", icon: "doc" },
  ];

  // Best-effort display name; fall back to the email's local part if the
  // backend hasn't returned a NomComplet (e.g. legacy seed accounts).
  const displayName =
    user?.nom?.trim() || user?.email?.split("@")[0] || "Utilisateur";
  const role = user?.role ?? "Plateforme PFA";
  return (
    <aside
      style={{
        width: W,
        height: "100%",
        minHeight: 0,
        flexShrink: 0,
        background:
          "linear-gradient(180deg, var(--side-bg) 0%, var(--side-bg-2) 100%)",
        color: "var(--side-text)",
        borderRight: "1px solid var(--side-border)",
        display: "flex",
        flexDirection: "column",
        transition: "width .18s ease",
        boxShadow: "inset -1px 0 0 rgba(255,255,255,0.02)",
      }}
    >
      {/* logo */}
      <div
        className="shrink-0 px-3 flex items-center gap-2.5 overflow-hidden"
        style={{
          height: 60,
          borderBottom: "1px solid var(--side-border)",
          background: "rgba(255,255,255,0.025)",
        }}
      >
        <img
          src="/eniad-logo.png"
          alt="ENIAD"
          style={{
            height: sidebarCollapsed ? 30 : 38,
            width: sidebarCollapsed ? 38 : "100%",
            maxWidth: "100%",
            objectFit: "contain",
            objectPosition: "left center",
            flexShrink: 1,
            transition: "height .18s ease, width .18s ease",
          }}
        />
      </div>

      {/* filiere context */}
      {!sidebarCollapsed && (
        <div
          className="shrink-0 px-3 py-3"
          style={{
            borderBottom: "1px solid var(--side-border)",
            position: "relative",
          }}
          ref={dropRef}
        >
          <div
            className="text-[10px] uppercase tracking-wider mb-1.5"
            style={{
              color: "var(--side-text-3)",
              fontWeight: 500,
              letterSpacing: "0.08em",
            }}
          >
            Périmètre
          </div>
          <button
            type="button"
            onClick={() => setDropOpen((o) => !o)}
            aria-expanded={dropOpen}
            aria-haspopup="listbox"
            aria-label={"Choisir le p\u00e9rim\u00e8tre de fili\u00e8re"}
            className="w-full flex items-center gap-2 px-2 py-1.5 rounded-md text-left text-[12.5px] transition"
            style={{
              background: "rgba(255,255,255,0.04)",
              color: "var(--side-text)",
              border: "1px solid rgba(255,255,255,0.06)",
            }}
          >
            <span
              style={{
                width: 18,
                height: 18,
                borderRadius: 4,
                background: "var(--accent-500)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: filiere === "TOUS" ? 7.5 : 9.5,
                fontWeight: 700,
                color: "#fff",
                flexShrink: 0,
              }}
            >
              {filiere === "TOUS" ? "ALL" : filiere}
            </span>
            <span className="flex-1 truncate">
              {filiere === "TOUS" ? "Toutes filières" : `Filière ${filiere}`}
            </span>
            <Icon
              name={dropOpen ? "chevUp" : "chevDown"}
              size={13}
              className="opacity-60"
            />
          </button>

          {dropOpen && (
            <div
              style={{
                position: "absolute",
                top: "100%",
                left: 12,
                right: 12,
                background: "var(--surface)",
                border: "1px solid var(--border)",
                borderRadius: 8,
                boxShadow: "0 8px 24px rgba(0,0,0,.25)",
                zIndex: 50,
                overflow: "hidden",
                marginTop: 4,
              }}
            >
              {FILIERES.map((f) => (
                <button
                  key={f}
                  onClick={() => {
                    setFiliere(f);
                    setDropOpen(false);
                  }}
                  className="w-full flex items-center gap-2.5 px-3 py-2 text-[12.5px] text-left transition"
                  style={{
                    background:
                      f === filiere
                        ? "color-mix(in oklch, var(--accent-500) 10%, transparent)"
                        : "transparent",
                    color: f === filiere ? "var(--accent-600)" : "var(--text)",
                    fontWeight: f === filiere ? 500 : 400,
                  }}
                  onMouseEnter={(e) => {
                    if (f !== filiere)
                      e.currentTarget.style.background = "var(--surface-2)";
                  }}
                  onMouseLeave={(e) => {
                    if (f !== filiere)
                      e.currentTarget.style.background = "transparent";
                  }}
                >
                  <span
                    style={{
                      width: 18,
                      height: 18,
                      borderRadius: 4,
                      background:
                        f === filiere
                          ? "var(--accent-500)"
                          : "var(--surface-2)",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontSize: 9,
                      fontWeight: 700,
                      color: f === filiere ? "#fff" : "var(--text-3)",
                      flexShrink: 0,
                    }}
                  >
                    {f}
                  </span>
                  {f === "TOUS" ? "Toutes filières" : `Filière ${f}`}
                  {f === filiere && (
                    <Icon
                      name="check"
                      size={12}
                      style={{ marginLeft: "auto", color: "var(--accent-500)" }}
                    />
                  )}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      <nav className="min-h-0 flex-1 overflow-y-auto scroll-thin px-3 py-3">
        {!sidebarCollapsed && (
          <div className="nav-section-title">
            {isEns ? "Enseignement" : "Pilotage"}
          </div>
        )}
        {PRIMARY.map((it) => (
          <NavLink
            key={it.to}
            to={it.to}
            className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}
            title={sidebarCollapsed ? it.label : undefined}
          >
            <Icon name={it.icon} size={15} />
            {!sidebarCollapsed && (
              <span className="flex-1 truncate">{it.label}</span>
            )}
            {!sidebarCollapsed && it.badge != null && (
              <span
                className={`pill pill-${it.badgeTone ?? "neutral"}`}
                style={{ fontSize: 10.5, padding: "1px 6px" }}
              >
                {it.badge}
              </span>
            )}
          </NavLink>
        ))}

        {!sidebarCollapsed && <div className="nav-section-title">Système</div>}
        {SECONDARY.filter(
          (it) => it.to !== "/admin" || user?.role === "Admin",
        ).map((it) => (
          <NavLink
            key={it.to}
            to={it.to}
            className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}
            title={sidebarCollapsed ? it.label : undefined}
          >
            <Icon name={it.icon} size={15} />
            {!sidebarCollapsed && (
              <span className="flex-1 truncate">{it.label}</span>
            )}
          </NavLink>
        ))}
      </nav>

      <div
        className="shrink-0 p-2.5"
        style={{
          borderTop: "1px solid var(--side-border)",
          position: "relative",
        }}
        ref={userRef}
      >
        {!sidebarCollapsed ? (
          <>
            {userMenuOpen && (
              <div
                style={{
                  position: "absolute",
                  bottom: "calc(100% + 8px)",
                  left: 10,
                  right: 10,
                  background: "var(--surface)",
                  border: "1px solid var(--border)",
                  borderRadius: 8,
                  boxShadow: "0 8px 24px rgba(0,0,0,.32)",
                  zIndex: 50,
                  overflow: "hidden",
                }}
              >
                <button
                  onClick={() => {
                    navigate("/settings");
                    setUserMenuOpen(false);
                  }}
                  className="w-full flex items-center gap-2.5 px-3 py-2 text-[12.5px] text-left transition"
                  style={{ color: "var(--text)" }}
                  onMouseEnter={(e) =>
                    (e.currentTarget.style.background = "var(--surface-2)")
                  }
                  onMouseLeave={(e) =>
                    (e.currentTarget.style.background = "transparent")
                  }
                >
                  <Icon
                    name="settings"
                    size={13}
                    style={{ color: "var(--text-3)" }}
                  />
                  Profil et paramètres
                </button>
                <button
                  onClick={() => {
                    logout();
                    setUserMenuOpen(false);
                  }}
                  className="w-full flex items-center gap-2.5 px-3 py-2 text-[12.5px] text-left transition"
                  style={{ color: "var(--bad)" }}
                  onMouseEnter={(e) =>
                    (e.currentTarget.style.background = "var(--surface-2)")
                  }
                  onMouseLeave={(e) =>
                    (e.currentTarget.style.background = "transparent")
                  }
                >
                  <Icon name="x" size={13} />
                  Déconnexion
                </button>
              </div>
            )}
            <button
              type="button"
              onClick={() => setUserMenuOpen((o) => !o)}
              aria-expanded={userMenuOpen}
              aria-haspopup="menu"
              className="w-full flex items-center gap-2.5 px-1.5 py-1.5 rounded-md"
              style={{
                transition: "background .12s",
                background: userMenuOpen
                  ? "rgba(255,255,255,0.06)"
                  : "transparent",
              }}
            >
              <Avatar name={displayName} size={26} />
              <div className="flex-1 overflow-hidden text-left">
                <div
                  className="text-[12px] font-medium truncate"
                  style={{ color: "#fff" }}
                >
                  {displayName}
                </div>
                <div
                  className="text-[10.5px] truncate"
                  style={{ color: "var(--side-text-3)" }}
                >
                  {role}
                </div>
              </div>
              <Icon
                name={userMenuOpen ? "chevUp" : "chevDown"}
                size={13}
                style={{ color: "var(--side-text-3)" }}
              />
            </button>
          </>
        ) : (
          <button
            onClick={() => navigate("/settings")}
            className="flex justify-center w-full"
            title="Paramètres"
            aria-label="Ouvrir les paramètres"
          >
            <Avatar name={displayName} size={28} />
          </button>
        )}
        <button
          type="button"
          onClick={toggleSidebar}
          aria-label={
            sidebarCollapsed
              ? "D\u00e9ployer la barre lat\u00e9rale"
              : "R\u00e9duire la barre lat\u00e9rale"
          }
          className="mt-2 w-full flex items-center justify-center gap-1.5 py-1.5 rounded-md text-[11px]"
          style={{ color: "var(--side-text-3)", background: "transparent" }}
          onMouseEnter={(e) =>
            (e.currentTarget.style.background = "rgba(255,255,255,0.04)")
          }
          onMouseLeave={(e) =>
            (e.currentTarget.style.background = "transparent")
          }
        >
          <Icon name={sidebarCollapsed ? "expand" : "collapse"} size={13} />
          {!sidebarCollapsed && <span>Replier</span>}
        </button>
      </div>
    </aside>
  );
}
