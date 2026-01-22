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
}