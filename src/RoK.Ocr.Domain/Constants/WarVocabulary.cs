namespace RoK.Ocr.Application.Reports.Constants;

public static class WarVocabulary
{
    // Adicionado: IT, TR, KR, VN, ID, TH, AR, CN (Simp/Trad)
    public static readonly string[] BattleAnchors = {
        // Originais
        "Relatorio de Batalha", "Battle Report", "戦闘報告", "Relatório de Batalha",
        "Battle Log", "Combat Report", "Bericht", "Rapport de bataille", "Боевой отчет",
        // Novos
        "战报", "戰報", // CN
        "전투 리포트", "전투 기록", // KR
        "Kampfbericht", // DE
        "Informe de batalla", "Reporte de batalla", // ES
        "Rapporto di battaglia", // IT
        "Savaş Raporu", // TR
        "Báo cáo trận chiến", // VN
        "Laporan Pertempuran", // ID
        "รายงานการรบ", // TH
        "تقرير المعركة" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] UnitsLabels = {
        // Originais
        "Unidades de Tropas", "Troops", "Unidades", "兵数", "Units",
        "Truppen", "Troupes", "Unidades de tropa", "Войска",
        // Novos
        "部队", "部隊", // CN
        "Truppe", "Unità", // IT
        "Birlikler", // TR
        "부대", "병력", // KR
        "Quân đội", "Đơn vị", // VN
        "Pasukan", "Unit", // ID
        "กองทหาร", // TH
        "القوات" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] DeadLabels = {
        // Originais
        "Morto", "Dead", "Todesfälle", "死者", "战死", "Tués", "Muertos", "Мертвые",
        // Novos
        "陣亡", // CN Trad
        "Morti", // IT
        "Ölü", // TR
        "전사", "사망", // KR
        "Tử trận", "Chết", // VN
        "Mati", "Tewas", // ID
        "เสียชีวิต", "ตาย", // TH
        "القتلى" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] SevereWoundedLabels = {
        // Originais
        "Gravemente ferido", "Gravemente ferida", "Severely Wounded", "重傷", "重伤",
        "Schwer verwundet", "Gravement blessés", "Heridos graves", "Тяжело ранены",
        // Novos
        "Feriti gravemente", // IT
        "Ağır Yaralı", // TR
        "중상", // KR
        "Trọng thương", // VN
        "Luka Parah", // ID
        "บาดเจ็บสาหัส", // TH
        "إصابة بليغة" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] SlightlyWoundedLabels = {
        // Originais
        "Levemente ferida", "Levemente ferido", "Slightly Wounded", "軽傷", "轻伤",
        "Leicht verwundet", "Légèrement blessés", "Heridos leves", "Легко ранены",
        // Novos
        "Feriti lievemente", // IT
        "Hafif Yaralı", // TR
        "경상", // KR
        "Khinh thương", // VN
        "Luka Ringan", // ID
        "บาดเจ็บเล็กน้อย", // TH
        "إصابة طفيفة" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] RemainingLabels = {
        // Originais
        "Restante", "Remaining", "残存数", "剩余", "Verbleibend", "Restantes", "Осталось",
        // Novos
        "剩餘", // CN Trad
        "Rimanenti", // IT
        "Kalan", // TR
        "잔여", "남은 병력", // KR
        "Còn lại", // VN
        "Tersisa", // ID
        "คงเหลือ", // TH
        "المتبقية" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] HealedLabels = {
        // Originais
        "Cura", "Healed", "Heal", "治疗", "Heilung", "Soin", "Curados", "Исцелено",
        // Novos
        "治療", // CN Trad
        "Guariti", // IT
        "İyileştirilen", // TR
        "치료", // KR
        "Đã chữa trị", // VN
        "Disembuhkan", // ID
        "รักษาแล้ว", // TH
        "تم الشفاء" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] WatchtowerLabels = {
        // Originais
        "Dano de Torre de Vigia", "Watchtower Damage", "警戒塔伤害", "警戒塔傷害",
        "Wachturm-Schaden", "Dégâts de tour de guet", "Урон сторожевой башни",
        // Novos
        "Danni torre di guardia", // IT
        "Gözcü Kulesi Hasarı", // TR
        "경계탑 피해", // KR
        "Sát thương tháp canh", // VN
        "Kerusakan Menara Pengawas", // ID
        "ดาเมจหอสังเกตการณ์", // TH
        "ضرر برج المراقبة" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] KillPointsLabels = {
        // Originais
        "Pontos de Abate", "Kill Points", "撃破ポイント", "击杀积分",
        "Tötungspunkte", "Points de kill", "Puntos de muerte",
        // Novos
        "擊殺積分", // CN Trad
        "Punti uccisione", // IT
        "Öldürme Puanı", // TR
        "처치 포인트", "킬 포인트", // KR
        "Điểm tiêu diệt", // VN
        "Poin Kill", // ID
        "คะแนนสังหาร", // TH
        "نقاط القتل" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] KillCountLabels = { 
        // Originais
        "Contagem de abates", "Kill count",
        // Novos
        "击杀数", "擊殺數", // CN
        "撃破数", // JP
        "Anzahl Tötungen", // DE
        "Nombre de victimes", // FR
        "Conteggio uccisioni", // IT
        "Recuento de muertes", // ES
        "Öldürme Sayısı", // TR
        "처치 수", // KR
        "Số lượng tiêu diệt", // VN
        "Jumlah Kill", // ID
        "จำนวนสังหาร", // TH
        "عدد القتلى" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] VictoryTerms = {
        // Originais
        "Vitoria", "Vitória", "Victory", "Vitoire", "勝利", "Sieg", "Victoria", "Победа",
        // Novos
        "胜利", // CN Simp
        "Vittoria", // IT
        "Zafer", // TR
        "승리", // KR
        "Chiến thắng", // VN
        "Kemenangan", // ID
        "ชนะ", // TH
        "نصر" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] DefeatTerms = {
        // Originais
        "Derrota", "Defeat", "Défaite", "敗北", "Niederlage", "Derrota", "Поражение",
        // Novos
        "失败", "失敗", // CN
        "Sconfitta", // IT
        "Yenilgi", // TR
        "패배", // KR
        "Thất bại", // VN
        "Kekalahan", // ID
        "พ่ายแพ้", // TH
        "هزيمة" // AR
    };

    // Adicionado: Termos comuns de UI para Blacklist (Tabs do sistema de correio e botões comuns)
    public static readonly string[] GlobalBlacklist = {
        // --- Portuguese (Originals) ---
        "PESSOAL", "RELATORIO", "ALIANCA", "SISTEMA", "ENVIADO", "FAVORITOS",
        "Nova mensagem", "Ordenar por categoria", "Relatorio de Explora",
        "Aldeia Tribal", "2minutosatras", "Horas atras", "Ataque cancelado",
        "Reembolso do pont", "Lere resgatar tudo", "Expandir o editor", "Compartilhar",

        // --- English ---
        "PERSONAL", "REPORT", "ALLIANCE", "SYSTEM", "SENT", "FAVORITES",
        "New Message", "Sort by Category", "Exploration Report", "Battle Report",
        "Tribal Village", "Attack Cancelled", "Point Refund", "Read and Claim All",
        "Expand", "Share", "ago", "Just now",

        // --- Chinese (Simplified & Traditional) ---
        "个人", "战报", "联盟", "系统", "已发送", "收藏", // CN Simp headers
        "個人", "戰報", "聯盟", "系統", "發送", // CN Trad headers
        "一键已读", "全部已读", "全部領取", "一鍵領取", // Read/Claim all buttons
        "部落村庄", "部落村莊", // Tribal Village

        // --- Japanese ---
        "個人", "報告", "同盟", "システム", "送信済", "お気に入り",
        "既読にして一括受取", "一括受取", // Read & Claim All
        "部族の村", // Tribal Village

        // --- Korean ---
        "개인", "리포트", "연맹", "시스템", "보낸 편지", "즐겨찾기",
        "모두 읽기 및 수령", "일괄 수령", // Read & Claim All
        "부족 마을", // Tribal Village

        // --- German ---
        "PERSÖNLICH", "BERICHT", "ALLIANZ", "GESENDET", "FAVORITEN",
        "Alles einsammeln", "Alles lesen", // Read/Claim
        "Stammesdorf", // Tribal Village

        // --- French ---
        "PERSONNEL", "RAPPORT", "SYSTÈME", "ENVOYÉS", "FAVORIS",
        "Tout récupérer", "Tout lire",
        "Village tribal",

        // --- Spanish ---
        "PERSONAL", "INFORME", "ALIANZA", "SISTEMA", "ENVIADO", "FAVORITOS",
        "Recoger todo", "Leer todo",
        "Aldea tribal",

        // --- Russian ---
        "ЛИЧНЫЕ", "ОТЧЕТ", "АЛЬЯНС", "СИСТЕМA", "ОТПРАВЛЕННЫЕ", "ИЗБРАННОЕ",
        "Прочесть и забрать все", // Read & Claim All
        "Племенная деревня", // Tribal Village

        // --- Turkish ---
        "KİŞİSEL", "RAPOR", "İTTİFAK", "SİSTEM", "GÖNDERİLEN", "FAVORİLER",
        "Hepsini Topla", // Claim All
        "Kabile Köyü", // Tribal Village

        // --- Vietnamese ---
        "CÁ NHÂN", "BÁO CÁO", "LIÊN MINH", "HỆ THỐNG", "ĐÃ GỬI", "MỤC YÊU THÍCH",
        "Thu thập tất cả", "Đọc tất cả",
        "Ngôi làng bộ lạc",

        // --- Indonesian ---
        "PRIBADI", "LAPORAN", "ALIANSI", "SISTEM", "TERKIRIM", "FAVORIT",
        "Klaim Semua", "Baca Semua",
        "Desa Suku",

        // --- Thai ---
        "ส่วนตัว", "รายงาน", "พันธมิตร", "ระบบ", "ส่งแล้ว", "รายการโปรด",
        "อ่านและรับทั้งหมด", // Read & Claim All
        "หมู่บ้านชนเผ่า", // Tribal Village

        // --- Arabic ---
        "شخصي", "تقرير", "تحالف", "نظام", "مرسلة", "مفضلة", // Tabs
        "قراءة والمطالبة بالكل", "المطالبة بالكل", // Read & Claim
        "قرية قبلية" // Tribal Village
    };

    public static readonly string[] UiBlacklist = {
        // --- Portuguese (Originals) ---
        "Pessoal", "Relatório", "Aliança", "Sistema", "Enviado", "Favoritos",
        "Ordenar", "Categoria", "Explora", "Batalha", "Vitoria", "Derrota",
        "Tropas", "Dano", "Recebido", "Abates",

        // --- English ---
        "Personal", "Report", "Alliance", "System", "Sent", "Favorites",
        "Sort", "Category", "Exploration", "Battle", "Victory", "Defeat",
        "Troops", "Damage", "Taken", "Kills", "Healed",

        // --- Chinese (Simplified & Traditional) ---
        "个人", "战报", "联盟", "系统", "已发送", "收藏",
        "分类", "排序", "探索", "战斗", "胜利", "失败", // Sort, Category, etc
        "部隊", "擊殺", "重傷", // Common UI labels

        // --- Japanese ---
        "個人", "報告", "同盟", "システム", "送信済", "お気に入り",
        "並べ替え", "カテゴリ", "探索", "戦闘", "勝利", "敗北",

        // --- Korean ---
        "개인", "리포트", "연맹", "시스템", "보낸 편지", "즐겨찾기",
        "정렬", "카테고리", "탐색", "전투", "승리", "패배",

        // --- Russian ---
        "Личные", "Отчет", "Альянс", "Система", "Отправленные", "Избранное",
        "Сорт.", "Категория", "Разведка", "Битва", "Победа", "Поражение",

        // --- German / French / Spanish (Common UI Terms) ---
        "Sortieren", "Kategorie", "Erkundung", "Sieg", "Niederlage", // DE
        "Trier", "Catégorie", "Exploration", "Victoire", "Défaite", // FR
        "Ordenar", "Categoría", "Exploración", "Victoria", "Derrota", // ES

        // --- Turkish ---
        "Sırala", "Kategori", "Keşif", "Zafer", "Yenilgi",

        // --- Vietnamese ---
        "Sắp xếp", "Danh mục", "Thăm dò", "Chiến thắng", "Thất bại",

        // --- Indonesian ---
        "Urutkan", "Kategori", "Jelajah", "Kemenangan", "Kekalahan",

        // --- Thai ---
        "จัดเรียง", "หมวดหมู่", "สำรวจ", "ชนะ", "พ่ายแพ้",

        // --- Arabic ---
        "فرز", "فئة", "استكشاف", "نصر", "هزيمة"
    };
    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] BarbarianKeywords = {
        // Originais
        "Barbaro", "Bárbaro", "Barbarian", "野蛮人", "Barbare", "Bárbaros",
        // Novos
        "野蠻人", // CN Trad
        "Barbar", // DE, ID
        "Barbaro", // IT
        "Barbarlar", // TR
        "Varvara", "Варвар", // RU
        "야만인", // KR
        "Người man rợ", // VN
        "คนเถื่อน", // TH
        "البربر" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] FortKeywords = {
        // Originais
        "Forte Barbaro", "Barbarian Fort", "野蛮人城寨", "Fort barbare",
        // Novos
        "野蠻人城寨", // CN Trad
        "Barbarenfestung", // DE
        "Fuerte bárbaro", // ES
        "Forte barbaro", // IT
        "Barbar Kalesi", // TR
        "Форт варваров", // RU
        "야만인 주둔지", // KR
        "Pháo đài người man rợ", // VN
        "Benteng Barbar", // ID
        "ป้อมคนเถื่อน", // TH
        "حصن البربر" // AR
    };

    // Adicionado: IT, TR, KR, VN, ID, TH, CN
    public static readonly string[] DamageLabels = {
        // Originais
        "Dano Recebido", "Damage Taken", "Danno ricevuto", "Dégâts reçus", "Урон получен",
        // Novos
        "受到伤害", "受到傷害", // CN
        "被ダメージ", // JP
        "Erlittener Schaden", // DE
        "Daño recibido", // ES
        "Hasar Alındı", // TR
        "피해를 입음", "받은 피해", // KR
        "Sát thương phải chịu", // VN
        "Damage Diterima", // ID
        "ได้รับดาเมจ", // TH
        "الضرر المتلقى" // AR
    };
}