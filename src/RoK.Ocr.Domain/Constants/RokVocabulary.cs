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
        // Menus and Buttons (PT/EN)
        "VIP", "Mais Informacoes", "More Info", "Perfil", "Profile",
        "Construir", "Build", "Recrutar", "Recruit", "Pesquisar", "Research",
        "Reparar", "Repair", "Chat", "Mensagem", "Message", "Comandante",
        "Commander", "Tropas", "Troops", "Conquistas", "Achievements",
        "Configuracoes", "Settings", "Ranking", "Classificacao", "Classificac",
        "Guia", "Guide", "Retrospecto", "Temporada", "Season", "UTC",
        "Câmara", "City Hall", "Prefeitura", "Hotel de Ville", "Rathaus",
        
        // Common International Terms (Russian, German, Chinese)
        "Настройки", // Settings (RU)
        "Einstellungen", // Settings (DE)
        "设置", // Settings (CN)
        "설정", // Settings (KR)
        
        // Events and Screen Labels
        "Campeoes", "Olimpia", "Olympia", "Arca", "Osiris", "Reino", "Perdido", "Lost Kingdom",
        "Vitorias", "Wins", "Victories", "Siege", "Victoires", "Siege", // DE/FR
        "Autarca", "Oculto", "Hidden", "N/A", "NIA",
        "Bronze", "Ferro", "Idade", "Age", "Feudal", "Dark", "Trevas"
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
}