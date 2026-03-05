namespace RoK.Ocr.Domain.Constants;

public static class RallyVocabulary
{
    // =================================================================
    // 0. CANONICAL STATES (Internal Standard Outputs)
    // =================================================================
    public const string StatePreparing = "Preparing";
    public const string StateMarching = "Marching";
    public const string StateArrived = "Arrived";
    public const string TargetFort = "Barbarian Fort";
    public const string TargetBarbarian = "Barbarian";

    // =================================================================
    // 1. HEADER ANCHORS
    // =================================================================
    public static readonly string[] CapacityLabels =
    {
        "Capacidade do Exército", "Army Capacity", "Capacidade",
        "Capacité", "Kapazität", "Capacidad",
        "集结部队容量", "군대 규모" // CN, KR
    };

    public static readonly string[] AllianceRallyLabels =
    {
        "Reunião da aliança", "Alliance Rally", "Rally",
        "Ralliement", "Sammeln", "Reunión",
        "联盟集结", "연맹 집결"
    };

    public static readonly string[] PreparingLabels =
    {
        "Preparando", "Preparing", "Préparation", "Vorbereitung", "Preparando",
        "准备中", "준비 중"
    };

    public static readonly string[] MarchingLabels =
    {
        "Marchando", "Marching", "En marche", "Marschieren", "Marchando",
        "行军中", "행군 중"
    };

    // =================================================================
    // 2. LIST ANCHORS
    // =================================================================
    public static readonly string[] TroopDetailsHeaders =
    {
        "Detalhes das tropas", "Troop Details", "Détails des troupes",
        "Truppendetails", "Detalles de tropa",
        "部队详情", "병력 상세"
    };

    public static readonly string[] UnitsLabels =
    {
        "Unidades:", "Units:", "Unités:", "Einheiten:", "Unidades:",
        "单位:", "유닛:"
    };

    public static readonly string[] ArrivedLabels =
    {
        "Chegou", "Arrived", "Arrivé", "Angekommen", "Llegó",
        "已到达", "도착"
    };

    // =================================================================
    // 3. UI BUTTONS (To Ignore/Filter)
    // =================================================================
    public static readonly string[] UiButtons =
    {
        "Cancelar", "Cancel", "Annuler", "Abbrechen", "ancelar",
        "Dissolver", "Disband", "Dissoudre", "Auflösen",
        "Mais Recente", "Newest", "Plus récent", "Neueste"
    };

    // =================================================================
    // 4. MAP OBJECTS & TARGETS
    // =================================================================
    public static readonly string[] FortKeywords =
    {
        "Forte Barbaro", "Barbarian Fort", "野蛮人城寨", "Fort barbare",
        "野蠻人城寨", "Barbarenfestung", "Fuerte bárbaro", "Forte barbaro",
        "Barbar Kalesi", "Форт варваров", "야만인 주둔지", "Pháo đài",
        "Benteng Barbar", "ป้อมคนเถื่อน", "حصن البربر"
    };

    public static readonly string[] BarbarianKeywords =
    {
        "Barbaro", "Bárbaro", "Barbarian", "野蛮人", "Barbare", "Bárbaros",
        "野蠻人", "Barbar", "Barbarlar", "Varvara", "Варвар",
        "야만인", "Người man rợ", "คนเถื่อน", "البربر"
    };

    public static readonly string[] StructureKeywords =
{
    "Bandeira da Aliança", "Alliance Flag", "联盟旗帜", "Drapeau d'alliance", "Bandera de la alianza",
    "Fortaleza da Aliança", "Alliance Fortress", "联盟要塞", "Forteresse d'alliance", "Fortaleza de la alianza",
    "Passagem", "Pass", "关卡", "Passage", "Paso",
    "Santuário", "Sanctuary", "圣所", "Sanctuaire", "Santuario",
    "Altar", "Altar", "祭坛", "Autel",
    "Templo Perdido", "Lost Temple", "失落的神庙", "Temple perdu"
};
}