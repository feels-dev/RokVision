namespace RoK.Ocr.Domain.Constants;

public static class RokVocabulary
{
    // =================================================================
    // 1. MAIN ANCHORS (To find the fields)
    // =================================================================

    public static readonly string[] GovernorLabels =
    { 
        // PT-BR / EN / ES / FR
        "Governador", "Governor", "Gouverneur", "Gobernador",
        "ID", "ID:", "(ID", "lD", "1D", // OCR variations for ID
        
        // German / Russian / Turkish
        "Statthalter", "Правитель", "Vali",
        
        // Asian / Arabic
        "执政官", // Chinese (Governor)
        "집정관", // Korean
        "領主",   // Japanese
        "الحاكم"  // Arabic
    };

    public static readonly string[] AllianceLabels =
    { 
        // PT-BR / EN / ES / FR
        "Alianca", "Alliance", "Alianza", "Aliança", 
        
        // German / Russian / Turkish
        "Allianz", "Альянс", "Ittifak",
        
        // Asian / Arabic
        "联盟", // Chinese
        "연맹", // Korean
        "同盟", // Japanese
        "التحالف" // Arabic
    };

    public static readonly string[] PowerLabels =
    { 
        // PT-BR / EN / ES / FR
        "Poder", "Power", "Puissance", "P0der", "Powcr", "Poder de Combate",
        
        // German / Russian / Turkish
        "Macht", "Мощь", "Guc", "Güç",
        
        // Asian / Arabic / Vietnamese
        "战力", "战斗力", // Chinese (Combat Power)
        "전투력",        // Korean
        "戦力",          // Japanese
        "القوة",         // Arabic
        "Sức mạnh"       // Vietnamese
    };

    public static readonly string[] KillPointsLabels =
    { 
        // PT-BR / EN / ES / FR
        "Pontos de Abate", "Kill Points", "Kills", "Abate", "Muertes",
        "Points de kill", "Troupes tuées",
        
        // German / Russian / Turkish
        "Tötungspunkte", "Очки убийств", "Oldurme Puani",
        
        // Asian / Arabic
        "击杀", "击杀积分", // Chinese
        "처치", "처치 포인트", // Korean
        "撃破",              // Japanese
        "نقاط القتل"         // Arabic
    };

    public static readonly string[] StatusLabels =
    {
        "Pontos de Acao", "Action Points", "AP", "Barra", "Nivel",
        "Stamina", "Energie", "Endurance"
    };

    // =================================================================
    // 2. PROHIBITED WORDS (UI / Buttons / Menus)
    // Used to avoid confusing button text with Player Name
    // =================================================================
    public static readonly string[] UiKeywords = new[]
    {

    "Governador", "Governor", "Governors", "ID:", "ID", "1D:", "ld:",
    "Civilização", "Civilizacao", "Civilizagao", "Civilization", "Civilizacion",
    "Aliança", "Alianca", "Alianga", "Alliance", "Alianza", "Alliance Tag",
    "Poder", "Power", "Poder de Combate", "Puissance", "Kraft", "Moch", "战力",
    "Pontos de Abate", "Kill Points", "Killpoints", "Puntos de Muerte", "Kills", "击杀",
    "Pontos de Ação", "Action Points", "Pontos de Acao", "Points d'action", "Aktionspunkte",

    "PERFIL DO GOVERNADOR", "GOVERNOR PROFILE", "PERFIL DEL GOBERNADOR",
    "Mais Informações", "Mais Informacoes", "More Info", "Plus d'infos", "Más información",
    "Conquistas", "Achievements", "Logros", "Succès", "Erfolge",
    "Classificação", "Classificacao", "Classificag", "Rankings", "Ranking", "Clasificación",
    "Comandante", "Commander", "Comandantes", "Commandant", "Tropas", "Troops", "Unidades",
    "Configurações", "Configuracoes", "Settings", "Ajustes", "Paramètres", "Einstellungen",
    "Mensagem", "Message", "Mensajes", "Chat", "Correio", "Mail",
    "Mural", "Wall", "City Wall", "Muralha",

    "Oculto", "Hidden", "Caché", "Oculto", "Privado",
    "N/A", "N/IA", "NIA", "None", "Nenhum",
    "Campeões de Olímpia", "Campeoes de Olimpia", "Olympia Champions", "Champions d'Olympia",
    "Arca de Osíris", "Arca de Osiris", "Ark of Osiris", "Arche d'Osiris",
    "O Reino Perdido", "The Lost Kingdom", "Le Royaume Perdu", "KVK",
    "Vitórias", "Vitorias", "Wins", "Victories", "Victórias", "Victoires", "Siege",
    "Autarca", "Autarch", "Inestimável", "Inestimavel", "Platina", "Platinum",
    "Retrospecto da Temporada", "Season Review", "Season", "Temporada",

    "VIP", "Ouro", "Gold", "Gemas", "Gems", "Gemas de Aliança",
    "Comida", "Food", "Milho", "Corn", "Madeira", "Wood", "Pedra", "Stone",
    "UTC", "X:", "Y:", "Localização", "Location", "Coordenadas",

    "Construir", "Build", "Recrutar", "Recruit", "Pesquisar", "Research", "Recherche",
    "Treinar", "Train", "Curar", "Heal", "Hospital", "Ajuda", "Help", "Aide",
    "Guia de jogabilidade", "Gameplay Guide", "Guide", "Guia",
    "Republic of Gamers", "ROG", "Space", "Enter", "Back", "Sair", "Exit",

    "Настройки", "Профиль", "Альянс", "Мощь", "Убийства", "Навыки",

    "Einstellungen", "Profil", "Allianz", "Kraft", "Kills", "Kommandant",

    "设置", "个人资料", "联盟", "战力", "击杀", "统帅", "角色",
    // Coreano
    "설정", "프로필", "연맹", "전투력", "처치", "사령관",

    "Ajustes", "Perfil", "Alianza", "Poder", "Muerte", "Configurazione", "Profilo",

    "Idade do Bronze", "Bronze Age", "Idade do Ferro", "Iron Age",
    "Idade das Trevas", "Dark Age", "Idade Feudal", "Feudal Age",
    "Era Industrial", "Industrial Era"
};

    // =================================================================
    // 3. CIVILIZATIONS (For detection and cleaning)
    // =================================================================
    public static readonly string[] CleanCivilizations =
    {
        // Europe / West
        "Roma", "Rome", "Rom", "Рим", // RU
        "Alemanha", "Germany", "Allemagne", "Deutschland", "Германия", // RU
        "Britania", "Britain", "Grande-Bretagne", "Britannien", "Британия", // RU
        "Franca", "France", "França", "Frankreich", "Франция", // RU
        "Espanha", "Spain", "Espagne", "Spanien", "Испания", // RU
        "Viking", "Vikings", "Wikinger", "Викинги", // RU
        "Grecia", "Greece", "Grece", "Griechenland", "Греция", // RU

        // East / Asia
        "China", "Chine", "Китай", "中国", "중국", // CN, KR
        "Japao", "Japan", "Japon", "Япония", "日本", // JP
        "Coreia", "Korea", "Coree", "Корея", "한국", // KR
        
        // Middle East / Africa
        "Arabia", "Arabie", "Аравия", "العربية", // AR
        "Otomano", "Ottoman", "Ottomane", "Osmanisches", "Османы",
        "Bizancio", "Byzantium", "Byzance", "Byzanz", "Виzantия",
        "Egito", "Egypt", "Egypte", "Ägypten", "Египет"
    };

    // =================================================================
    // 4. MAP & WORLD OBJECTS (To filter out non-city labels)
    // =================================================================
    public static readonly string[] MapMapObjects =
    {
        // Levels
        "Lvl", "Nivel", "Niveau", "Level", "Ur.", "Stufe", "Ур.", "Lv.", "Lv",
        
        // Barbarians & Forts
        "Barbarian", "Barbaro", "Barbare", "Bárbaro", "Barbar", "Варвар", "野蛮人", "야만인", "คนเถื่อน",
        "Fort", "Forte", "Festung", "Fortaleza", "Цитадель", "Pháo đài", "Benteng", "ป้อม",
        
        // Resources (Wood, Food, Stone, Gold, Gems)
        "Madeira", "Wood", "Bois", "Holz", "Madera", "Дерево", "木材", "목재", "Kayu", "ไม้",
        "Comida", "Food", "Nourriture", "Nahrung", "Comida", "Еда", "食物", "식량", "Makanan", "อาหาร",
        "Pedra", "Stone", "Pierre", "Stein", "Piedra", "Камень", "石料", "석재", "Batu", "หิน",
        "Ouro", "Gold", "Or", "Gold", "Oro", "Золото", "金币", "금화", "Emas", "ทอง",
        "Gemas", "Gems", "Gemmes", "Edelsteine", "Gemas", "Самоцветы", "宝石", "보석", "Permata", "อัญมณี",
        
        // Map UI / Markers
        "Alliance", "Alianca", "Territory", "Territorio", "Territoire", "Territorium", "Территория", "领土", "영토",
        "Marker", "Marcador", "Marqueur", "Markierung", "Маркер"
    };

    // =================================================================
    // 5. MAP UI BLOCKLIST (To strictly filter out HUD elements)
    // =================================================================
    public static readonly string[] MapUiBlocklist =
{
        // ═══════════════════════════════════════════════════════════
        // BOTTOM NAVIGATION MENU
        // ═══════════════════════════════════════════════════════════
        "Campanha", "Campaign", "Kampagne", "Campagne", "Campaña",
        "Itens", "Items", "Gegenstände", "Objets", "Artículos", "tens",
        "Alianca", "Aliança", "Alliance", "Allianz", "Alianza",
        "Comandante", "Commander", "Kommandant", "Commandant", "Comandantes",
        "Mensagem", "Message", "Nachricht", "Mensaje", "Chat",

        // ═══════════════════════════════════════════════════════════
        // SIDE QUEST PANEL (Left Side)
        // ═══════════════════════════════════════════════════════════
        "Irmaos de Armas", "Irmãos de Armas", "Brothers in Arms",
        "Crie uma alianca", "Crie uma aliança", "Create an alliance",
        "participe de uma", "participate in",
        "Eu Protejo Voce", "Eu Protejo Você", "I Protect You",
        "Ajude seus aliados", "Help your allies",
        "vezes", "times",
        "Terra da Civilizacao", "Terra da Civilização", "Civilization Land",
        "A Cupula", "A Cúpula", "The Dome",
        "O Retorno do Rei", "The King's Return",
        "Aprimore a Prefeitura", "Upgrade City Hall",
        "Derrote", "Defeat", "tropas barbaras", "barbarian troops",
        
        // ═══════════════════════════════════════════════════════════
        // MAP INTERACTION MENU
        // ═══════════════════════════════════════════════════════════
        "Entrar", "Enter", "Betreten", "Entrer",
        "Explorar", "Explore", "Erkunden", "Explorer",
        "Atacar", "Attack", "Angreifen", "Attaquer",
        "Reunir", "Rally", "Sammeln", "Rallier",
        "Comando rapido", "Comando rápido", "Quick Command",
        "Protejo", "Protect", "Schützen", "Protéger",

        // ═══════════════════════════════════════════════════════════
        // HUD ELEMENTS (Top)
        // ═══════════════════════════════════════════════════════════
        "VIP", "UTC", "X:", "Y:", "KM", "M",
        "Localizacao", "Localização", "Location",
        
        // ═══════════════════════════════════════════════════════════
        // COMMON FALSE POSITIVES (Generic UI)
        // ═══════════════════════════════════════════════════════════
        "Missao", "Missão", "Mission", "Quest",
        "Reino", "Kingdom", "Royaume",
        "Beneficio", "Benefício", "Buff", "Benefit",
        "Nivel", "Nível", "Level", "Lvl",
        
        // ═══════════════════════════════════════════════════════════
        // NUMBERS AND RATIOS (Quest Progress)
        // ═══════════════════════════════════════════════════════════
        "(0/5)", "(5/5)", "(1/3)", "0/5", "5/5",
        
        // ═══════════════════════════════════════════════════════════
        // RESOURCES & UI ICONS
        // ═══════════════════════════════════════════════════════════
        "Comida", "Food", "Madeira", "Wood", "Pedra", "Stone",
        "Ouro", "Gold", "Gemas", "Gems",
        
        // ═══════════════════════════════════════════════════════════
        // MAP OBJECTS (Not Player Cities)
        // ═══════════════════════════════════════════════════════════
        "Barbarian", "Barbaro", "Bárbaro", "Barbare",
        "Fort", "Forte", "Festung", "Fortaleza",
        "Territory", "Territorio", "Território", "Territorium",
        
        // ═══════════════════════════════════════════════════════════
        // ADDITIONAL LANGUAGES (Asian/Arabic)
        // ═══════════════════════════════════════════════════════════
        "战役", "道具", "联盟", "指挥官", "消息", // Chinese
        "캠페인", "아이템", "연맹", "사령관", "메시지", // Korean
        "الحملة", "العناصر", "التحالف", "القائد", "الرسالة", // Arabic

            // Portuguese
    "Comando", "rapido", "Campanha", "Itens", "Alianca", "Comandante", "Mensagem",
    "Irmaos de Armas", "Crie uma alianca", "participe de uma", "Eu Protejo Voce",
    "Ajude seus aliados", "Terra da Civilizacao", "Derrote", "tropas barbaras",
    "A Cupula", "Aprimore a Prefeitura", "Nivel", "Retorno do Rei",
    "Bom te ver de novo", "Rise of Kingdoms", "governador",
    
    // English
    "Command", "Quick", "Campaign", "Items", "Alliance", "Commander", "Message",
    "Brothers in Arms", "Create an alliance", "join one", "I Protect You",
    "Help your allies", "Land of Civilization", "Defeat", "barbarian troops",
    "The Dome", "Upgrade City Hall", "Level", "Return of the King",
    "Good to see you again", "governor",
    
    // Spanish
    "Comando", "rapido", "Campaña", "Articulos", "Alianza", "Comandante", "Mensaje",
    
    // Common UI
    "VIP", "UTC", "KM",

            // --- Bottom Menu & Panels ---
        "Comando", "Command", "Campanha", "Campaign", "Itens", "Items",
        "Alianca", "Alliance", "Mensagem", "Message", "Comandante", "Commander",
        "Rapido", "Quick", "Chat", "System", "Sistema",
        "Campagne", "Allianz", "Nachricht", "Objets", // DE/FR

        // --- Top HUD ---
        "VIP", "UTC", "KM", "Power", "Poder", "Might",

        // --- Side Quests / Events ---
        "Irmaos", "Brothers", "Protejo", "Protect", "Civiliza", "Civilization",
        "Derrote", "Defeat", "Barbaras", "Barbarian", "Cupula", "Dome",
        "Prefeitura", "City Hall", "Retorno", "Return", "Rei", "King",
        "Give_up", "Destroy", "Mark", "Guardians", "Castles", "Start", "Help",
        "Gracia", "Cry", "Thanks", "Obrigado", "Merci", "Danke", "Gg",
        
        // --- Asian / Arabic UI ---
        "战役", "道具", "联盟", "指挥官", "消息", // CN
        "캠페인", "아이템", "연맹", "사령관", "메시지", // KR
        "الحملة", "العناصر", "التحالف", "القائد", "الرسالة" // AR

    };

    public static readonly string[] MapNoiseKeywords =
    {
        "/", ":", "...", "…",
        "M", "K", // Resource suffixes if combined with numbers
        "Lvl", "Lv.", "Nivel"
    };

    // Keywords strictly found at the TOP (HUD)
    public static readonly string[] TopUiAnchors =
    {
        "VIP", "UTC", "KM", "Power", "Poder", "Might", "Governador", "Governor"
    };

    // Keywords strictly found at the BOTTOM (Menu)
    public static readonly string[] BottomUiAnchors =
    {
        "Comando", "Command", "Campanha", "Campaign", "Itens", "Items",
        "Alianca", "Alliance", "Mensagem", "Message", "Comandante", "Commander",
        "Campagne", "Allianz", "Nachricht", "Objets"
    };

    // Keywords for Chat/Action Logs (Dynamic Exclusion Areas)
    public static readonly string[] ChatKeywords =
    {
        "Rapido", "Quick", "Chat", "System", "Sistema",
        "Give_up", "Destroy", "Mark", "Guardians", "Castles", "Start", "Help",
        "Gracia", "Cry", "Thanks", "Obrigado", "Merci", "Danke", "Gg",
        ":", "..."
    };
}