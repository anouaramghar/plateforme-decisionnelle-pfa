"""
Curriculum definitions for all ENIAD programs.

Scraped and transcribed from:
  https://eniad.ump.ma/fr/formation-initiale
  (module screenshots for each program)

Used by:
  - Synthetic data generators to set realistic nb_modules per semester
  - Database seeding scripts to populate DimModule
"""

EPSI = {
    "code": "EPSI",
    "name": "Etudes Preparatoires en Sciences de l'Ingenieur",
    "semesters": {
        "S1": [
            "ALGEBRE 1",
            "ANALYSE 1",
            "MECANIQUE DU POINT",
            "ELECTROCINETIQUE",
            "ALGORITHMIQUE ET ARCHITECTURE DES ORDINATEURS",
            "METHODOLOGIE DE TRAVAIL UNIVERSITAIRE",
            "LANGUES ET COMMUNICATIONS 1",
        ],
        "S2": [
            "ALGEBRE 2",
            "ANALYSE 2",
            "ELECTROMAGNETISME",
            "ELECTRONIQUE ANALOGIQUE",
            "PROGRAMMATION EN C",
            "CULTURE DIGITALE",
            "LANGUES ET COMMUNICATIONS 2",
        ],
        "S3": [
            "ALGEBRE 3",
            "ANALYSE 3",
            "TRAITEMENT DU SIGNAL ET SYSTEMES NUMERIQUES",
            "ALGORITHMIQUE AVANCE & STRUCTURE DES DONNEES",
            "PROGRAMMATION PYTHON",
            "SYSTEMES D'INFORMATIONS ET BASES DE DONNEES",
            "LANGUES ET COMMUNICATIONS 3",
        ],
        "S4": [
            "ANALYSE 4",
            "STATISTIQUES & PROBABILITES",
            "INTRODUCTION AUX RESEAUX INFORMATIQUES ET SYSTEMES D'EXPLOITATION",
            "ANALYSE NUMERIQUE",
            "DEVELOPPEMENT WEB",
            "ELECTRONIQUE NUMERIQUE",
            "LANGUES ET COMMUNICATIONS 4",
        ],
    },
}

IRSI = {
    "code": "IRSI",
    "name": "Ingenierie Reseaux et Securite Informatique",
    "semesters": {
        "S5": [
            "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
            "PROGRAMMATION ORIENTE OBJET EN PYTHON",
            "SYSTEME D'EXPLOITATION ET PROGRAMMATION SYSTEMES",
            "RESEAUX INFORMATIQUE",
            "DEVELOPPEMENT D'APPLICATIONS WEB",
            "PROGRAMMATION ORIENTE OBJET EN JAVA",
            "COMPTABILITE ET CALCUL DES COUTS",
            "Langues et Techniques de Communication 1",
        ],
        "S6": [
            "ANALYSE DE DONNEES",
            "RECHERCHE OPERATIONNELLE ET OPTIMISATION COMBINATOIRE",
            "ADMINISTRATION SYSTEMES LINUX",
            "INTERCONNEXION DES RESEAUX",
            "INGENIERIE DES BASES DE DONNEES AVANCEE",
            "PROGRAMMATION SHELL & POWERSHELL",
            "INGENIERIE DU PROMPTING",
            "Langues et Techniques de Communication 2",
        ],
        "S7": [
            "INTERCONNEXION DES RESEAUX AVANCEE",
            "ADMINISTRATION ET SECURITE DES SERVICES",
            "GESTION AGILE DE PROJET INFORMATIQUE",
            "MACHINE LEARNING",
            "CRYPTOGRAPHIE : PROTOCOLES ET APPLICATIONS",
            "RESEAUX DE COMMUNICATION IOT",
            "MANAGEMENT ET MARKETING",
            "Langues et Techniques de Communication 3",
        ],
        "S8": [
            "SECURITE DES RESEAUX",
            "ATELIER DES ACTIVITES PRATIQUES ET PROJETS",
            "CLOUD COMPUTING",
            "DEEP LEARNING",
            "APPRENTISSAGE PAR RENFORCEMENT",
            "CYBERSECURITE",
            "DEVELOPPEMENT PERSONNEL",
            "Langues et Techniques de Communication 4",
        ],
        "S9": [
            "ATELIER PENTESTING WEB",
            "ATELIER ETHICAL HACKING",
            "ATELIER FIREWALL",
            "TECHNOLOGIE BLOCKCHAIN",
            "GOUVERNANCE DE LA SECURITE ET ANALYSE DES RISQUES",
            "ATELIER DEVSECOPS & SOC",
            "ETHIQUES ET DROITS",
            "Langues et Techniques de Communication 5",
        ],
    },
}

IA = {
    "code": "IA",
    "name": "Intelligence Artificielle",
    "semesters": {
        "S5": [
            "PROGRAMMATION ORIENTE OBJET EN JAVA",
            "PROGRAMMATION ORIENTE OBJET EN PYTHON",
            "INGENIERIE DES BASES DE DONNEES AVANCEE",
            "SYSTEME D'EXPLOITATION ET PROGRAMMATION SYSTEMES",
            "DEVELOPPEMENT D'APPLICATIONS WEB",
            "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
            "COMPTABILITE ET CALCUL DES COUTS",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 1",
        ],
        "S6": [
            "DEVELOPPEMENT D'APPLICATIONS WEB AVANCEE",
            "MACHINE LEARNING",
            "RESEAUX INFORMATIQUE",
            "MODELISATION LOGICIELLE ET DONNEES STRUCTUREES",
            "ANALYSE DE DONNEES",
            "RECHERCHE OPERATIONNELLE",
            "INGENIERIE DU PROMPTING",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 2",
        ],
        "S7": [
            "DEEP LEARNING",
            "VISION ARTIFICIELLE",
            "GESTION AGILE DE PROJET INFORMATIQUE",
            "DEVELOPPEMENT MOBILE MULTIPLATEFORME",
            "OPTIMISATION COMBINATOIRE ET METAHEURISTIQUES",
            "RESEAUX DE COMMUNICATION IOT",
            "MANAGEMENT ET MARKETING",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 3",
        ],
        "S8": [
            "SYSTEMES MULTI-AGENTS",
            "BUSINESS INTELLIGENCE ET ERP",
            "ATELIER DES ACTIVITES PRATIQUES ET PROJETS",
            "MODELES DE LANGAGE ET TRAITEMENT AUTOMATIQUE DU TEXTE",
            "VISION PAR ORDINATEUR ET INTELLIGENCE ARTIFICIELLE",
            "APPRENTISSAGE PAR RENFORCEMENT",
            "DEVELOPPEMENT PERSONNEL",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 4",
        ],
        "S9": [
            "INGENIERIE BIG DATA",
            "DEVOPS & MLOPS",
            "ATELIERS IA AVANCEE",
            "CLOUD COMPUTING ET VIRTUALISATION",
            "CYBERSECURITY POUR L'IA ET LA ROBOTIQUE",
            "EXPLAINABLE AI",
            "ETHIQUES ET DROITS",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 5",
        ],
    },
}

ROC = {
    "code": "ROC",
    "name": "Robotique et Objets Connectes",
    "semesters": {
        "S5": [
            "PROGRAMMATION ORIENTE OBJET EN JAVA",
            "PROGRAMMATION EMBARQUEE",
            "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
            "DEVELOPPEMENT D'APPLICATIONS WEB",
            "SYSTEME D'EXPLOITATION ET PROGRAMMATION SYSTEMES",
            "PERCEPTION ET CAPTEURS POUR OBJETS ET ROBOTS CONNECTES",
            "COMPTABILITE ET CALCUL DES COUTS",
            "Langues et Techniques de Communication 1",
        ],
        "S6": [
            "DEVELOPPEMENT D'APPLICATIONS WEB AVANCEE",
            "ROBOT OPERATING SYSTEM (ROS 1 & 2, RTOS)",
            "RESEAUX INFORMATIQUE",
            "METHODOLOGIES DE NAVIGATION ET DE LOCALISATION DES ROBOTS",
            "ANALYSE DES DONNEES",
            "RECHERCHE OPERATIONNELLE ET OPTIMISATION COMBINATOIRE",
            "INGENIERIE DU PROMPTING",
            "Langues et Techniques de Communication 2",
        ],
        "S7": [
            "PROGRAMMATION EN ROBOTIQUE ET CONCEPTION 3D",
            "RESEAUX DE COMMUNICATION IOT",
            "GESTION AGILE DE PROJET INFORMATIQUE",
            "DEVELOPPEMENT MOBILE MULTIPLATEFORME",
            "MACHINE LEARNING",
            "VISION ARTIFICIELLE",
            "MANAGEMENT ET MARKETING",
            "Langues et Techniques de Communication 3",
        ],
        "S8": [
            "SYSTEMES MULTI-AGENTS",
            "BUSINESS INTELLIGENCE ET ERP",
            "IA EMBARQUEE & EDGE AI",
            "DEEP LEARNING",
            "REINFORCEMENT LEARNING",
            "ATELIER DES ACTIVITES PRATIQUES ET PROJETS",
            "DEVELOPPEMENT PERSONNEL",
            "Langues et Techniques de Communication 4",
        ],
        "S9": [
            "INGENIERIE BIG DATA",
            "REALITE VIRTUEL ET REALITE AUGMENTEE",
            "ATELIER ROBOTIQUE AVANCEE (COBOTIQUE, MOBILITE)",
            "TECHNOLOGIES POUR L'AUTOMOBILE, L'AERONAUTIQUE ET LES DRONES",
            "CLOUD COMPUTING ET VIRTUALISATION",
            "CYBERSECURITY POUR L'IA ET LA ROBOTIQUE",
            "ETHIQUES ET DROITS",
            "Langues et Techniques de Communication 5",
        ],
    },
}

GINF = {
    "code": "GINF",
    "name": "Genie Informatique",
    "semesters": {
        "S5": [
            "PROGRAMMATION ORIENTE OBJET EN JAVA",
            "PROGRAMMATION ORIENTE OBJET EN PYTHON",
            "INGENIERIE DES BASES DE DONNEES AVANCEE",
            "DEVELOPPEMENT D'APPLICATIONS WEB",
            "SYSTEMES D'EXPLOITATION ET PROGRAMMATION SYSTEME",
            "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
            "COMPTABILITE ET CALCUL DES COUTS",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 1",
        ],
        "S6": [
            "PROGRAMMATION ORIENTE OBJET EN C++",
            "MODELISATION LOGICIELLE ET DONNEES STRUCTUREES",
            "DEVELOPPEMENT D'APPLICATIONS WEB AVANCEE",
            "RESEAUX INFORMATIQUES",
            "ANALYSE DES DONNEES",
            "RECHERCHE OPERATIONNELLE ET OPTIMISATION COMBINATOIRE",
            "INGENIERIE DU PROMPTING",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 2",
        ],
        "S7": [
            "INGENIERIE J2E ET APPLICATIONS DISTRIBUEES",
            "DEVELOPPEMENT D'APPLICATION .NET",
            "DEVELOPPEMENT MOBILE MULTIPLATEFORME",
            "ADMINISTRATION DES SYSTEMES",
            "MACHINE LEARNING",
            "GESTION AGILE DE PROJET INFORMATIQUE",
            "MANAGEMENT ET MARKETING",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 3",
        ],
        "S8": [
            "INGENIERIE DEVOPS",
            "ADMINISTRATION DE BASES DE DONNEES",
            "DEEP LEARNING",
            "APPRENTISSAGE PAR RENFORCEMENT",
            "BUSINESS INTELLIGENCE ET ERP",
            "ATELIER DES ACTIVITES PRATIQUES ET PROJETS",
            "DEVELOPPEMENT PERSONNEL",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 4",
        ],
        "S9": [
            "URBANISATION DES SYSTEMES D'INFORMATION",
            "ARCHITECTURE LOGICIELLE ET DESIGN PATTERNS",
            "INGENIERIE BIG DATA",
            "CLOUD COMPUTING ET VIRTUALISATION",
            "ATELIER PENTESTING WEB",
            "INTERCONNEXION RESEAUX ET SECURITE RESEAUX",
            "ETHIQUES ET DROITS",
            "LANGUES ET TECHNIQUES DE COMMUNICATION 5",
        ],
    },
}


PROGRAMS = {
    "EPSI": EPSI,
    "IRSI": IRSI,
    "IA": IA,
    "ROC": ROC,
    "GINF": GINF,
}

ALL_SEMESTERS = ["S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8", "S9"]

# Mapping: which semesters belong to each program
PROGRAM_SEMESTERS = {
    "EPSI": ["S1", "S2", "S3", "S4"],
    "IRSI": ["S5", "S6", "S7", "S8", "S9"],
    "IA": ["S5", "S6", "S7", "S8", "S9"],
    "ROC": ["S5", "S6", "S7", "S8", "S9"],
    "GINF": ["S5", "S6", "S7", "S8", "S9"],
}


def get_module_count(program_code: str, semester: str) -> int:
    prog = PROGRAMS.get(program_code)
    if prog is None:
        return 7
    modules = prog["semesters"].get(semester)
    return len(modules) if modules else 7


def get_module_names(program_code: str, semester: str) -> list[str]:
    prog = PROGRAMS.get(program_code)
    if prog is None:
        return []
    return list(prog["semesters"].get(semester, []))


def get_program_semesters(program_code: str) -> list[str]:
    return list(PROGRAM_SEMESTERS.get(program_code, []))
