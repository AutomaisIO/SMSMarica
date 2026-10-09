namespace SMSMais.Core.Integracoes.SisregWeb.Rede;

/// <summary>
/// Verificação do endereço do SISREG (<see cref="VerificadorEnderecoSisregWorker"/>). Em 09/10/2026 o
/// SISREG trocou de IP sem aviso e a produção ficou fora do túnel — "parecia que o nosso IP tinha sido
/// bloqueado". Pedido do Bernardo: "precisamos ter um verificador desses IPs do SISREG para não cair
/// nessa armadilha de novo… e registrar em algum lugar visível que o IP alterou dia tal e o IP atual".
/// </summary>
public sealed class EnderecoSisregOpcoes
{
    public const string Secao = "Sisreg:Endereco";

    public bool Habilitado { get; set; } = true;

    /// <summary>Nome resolvido. O mesmo da Base URL padrão do motor (<c>SisregWebSessao</c>).</summary>
    public string Host { get; set; } = "sisregiii.saude.gov.br";

    /// <summary>
    /// De quanto em quanto tempo verificar. O timer do servidor que conserta a rota roda a cada 1 min;
    /// 2 min aqui garante que a segunda verificação depois de uma troca já pega a rota consertada.
    /// </summary>
    public int IntervaloMinutos { get; set; } = 2;

    /// <summary>
    /// Interface do túnel por onde o SISREG tem de sair (docs/sisreg-egress.md). Só é EXIGIDA se existir
    /// neste servidor (<c>/sys/class/net/&lt;nome&gt;</c>): numa instância que sai direto pelo Brasil
    /// (ex.: VM na EVEO) não há túnel e a saída direta é o normal.
    /// </summary>
    public string InterfaceTunel { get; set; } = "wg-eveo";

    /// <summary>
    /// IP que some do DNS só encerra o período depois deste tempo sem aparecer — se um dia o DNS
    /// alternar entre IPs, o histórico não vira um pisca-pisca de trocas.
    /// </summary>
    public int MinutosParaEncerrar { get; set; } = 30;
}
