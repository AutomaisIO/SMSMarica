namespace SMSMais.Data.Entities;

/// <summary>
/// O que uma unidade executa de imagem, e como. É o nível que faltava entre o catálogo de
/// procedimentos (do município) e o aparelho (da unidade).
///
/// <para><b>Por que existe.</b> <see cref="TipoExame.EnviarParaWorklist"/> era um interruptor
/// único do município inteiro, e por isso não tinha estado correto: quando o CDT ganhou o raio-X
/// em 09/2026, ligar o tipo servia o CDT e fazia toda solicitação de Hospital Santo Antônio,
/// Ernesto Che Guevara e DIMAGEM — que executam radiografia e <b>não têm equipamento</b> — falhar
/// com <c>worklist.sem_equipamento</c>. Deixar desligado mantinha o raio-X novo sem worklist. O
/// mesmo bit precisava valer <c>false</c> numa unidade e <c>true</c> na outra.</para>
///
/// <para><b>Alcance: destino DICOM, só.</b> Responde "quando um exame deste tipo for executado
/// aqui, vai para qual máquina e entra na worklist". <b>Não</b> é gate de solicitação nem de
/// agendamento — quem oferece o quê ao cidadão é a oferta derivada da escala (ADR-0055), e duas
/// fontes de verdade sobre isso divergiriam.</para>
///
/// <para><b>Por que cadastrada e não derivada.</b> O ADR-0055 §5 deriva a oferta interna de
/// <c>sisreg_escala</c>. Medido em 08/09/2026, isso não serve para imagem: dos 298 pares
/// (unidade × tipo) que existem de fato, só <b>23 (7,7%)</b> aparecem na escala da mesma unidade —
/// escala do SISREG é de profissional/consulta, e exame de imagem chega por outros caminhos
/// (demanda espontânea, solicitação manual). A matriz é esparsa: 298 de 36.108 combinações
/// possíveis, e só 62 em unidades que têm aparelho.</para>
/// </summary>
public class TipoExameUnidade
{
    public Guid Id { get; set; }

    public Guid TipoExameId { get; set; }
    public TipoExame? TipoExame { get; set; }

    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>
    /// Se o exame deste tipo, executado NESTA unidade, vai para a Modality Worklist do PACS.
    /// Nasce <c>false</c>: mandar item mal formado ao equipamento é pior do que não mandar, e a
    /// associação desligada é justamente a fila de trabalho da tela "Exames a configurar".
    /// </summary>
    public bool EnviarParaWorklist { get; set; }

    /// <summary>
    /// Aparelho de destino, quando a unidade quer amarrar. Opcional de propósito — <c>null</c> cai
    /// na dedução por modalidade e a recepção escolhe a sala, que é o caso dos dois ultrassons do
    /// CDT.
    ///
    /// <para>Preenchido, resolve dois problemas: mata a pergunta repetida da recepção quando a
    /// resposta é fixa, e tira a modalidade do caminho crítico. Foi o casamento por modalidade que,
    /// em 04/09/2026, fez cinco radiografias marcadas como <c>MG</c> apontarem para o mamógrafo —
    /// 165 exames a um clique da sala errada.</para>
    /// </summary>
    public Guid? EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }

    /// <summary>Esta unidade executa este exame. Soft-delete via <see cref="ExcluidoEm"/>.</summary>
    public bool Ativo { get; set; } = true;

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
