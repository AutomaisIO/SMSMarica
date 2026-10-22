namespace SMSMais.Core.Cidadao.Push;

/// <summary>
/// Telas do app do cidadão que uma notificação pode abrir ("abrir em"). Lista FIXA, igual no
/// painel (select) e no app (que confere de novo antes de navegar): o servidor recusa qualquer
/// outra coisa, para o texto da equipe nunca virar um caminho arbitrário dentro do app.
/// </summary>
public static class RotasAppCidadao
{
    public static readonly IReadOnlyDictionary<string, string> Permitidas = new Dictionary<string, string>
    {
        ["/"] = "Início",
        ["/agendados/consultas"] = "Consultas agendadas",
        ["/agendados/exames"] = "Exames agendados",
        ["/atendimentos"] = "Meus atendimentos",
        ["/exames"] = "Exames",
        ["/documentos"] = "Documentos",
        ["/transporte"] = "Transporte (TFD)",
        ["/chat"] = "Chat",
        ["/perfil"] = "Meu perfil",
    };

    public static bool EhPermitida(string rota) => Permitidas.ContainsKey(rota);
}
