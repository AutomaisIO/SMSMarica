namespace SMSMais.Data.Entities;

/// <summary>
/// Um dos procedimentos de uma <see cref="Solicitacao"/> — os itens 1..N do pedido, como no papel
/// do SISREG (ticket #149).
///
/// <para><b>Por que existe:</b> uma solicitação do SISREG pode trazer mais de um procedimento (ex.:
/// RX de joelho DIREITO e ESQUERDO no mesmo nº). No TXT eles vêm como linhas com o mesmo
/// <c>codigo_solicitacao</c> e procedimento diferente. Com o procedimento guardado só nos campos da
/// espinha, a 2ª linha sobrescrevia a 1ª e ainda virava "alteração de procedimento" — medido em
/// 06/10/2026: 971 trocas falsas em 925 solicitações, até 9 na mesma (pedido de ~10 itens).</para>
///
/// <para><b>Os campos escalares da espinha continuam valendo</b> e são o procedimento PRINCIPAL
/// (<see cref="Ordem"/> = 1): dezenas de telas, o robô e as confirmações leem
/// <c>Solicitacao.ProcedimentoTexto</c>. Esta tabela não os substitui, completa.</para>
///
/// <para><b>Sem carga retroativa:</b> solicitação sem itens é lida como um item só, montado dos
/// escalares. O 1º item é materializado na hora em que o 2º procedimento aparece.</para>
///
/// <para>Data e executante são guardados POR ITEM porque cada linha do SISREG traz os seus. Na
/// prática eles coincidem: nos 2.986 pedidos com mais de um procedimento dos arquivos da implantação
/// (16/10/2026), todas as linhas tinham a mesma data, hora, unidade e chegada — o SISREG trata o
/// pedido como um só (cancela, marca presença e falta no pedido inteiro).</para>
///
/// <para>Cada item de <see cref="Ordem"/> ≥ 2 de um pedido de imagem gera o seu próprio
/// <see cref="ExameImagem"/> (accession com sufixo <c>-1</c>, <c>-2</c>…), que vai à worklist com o
/// código interno deste item — ADR-0074.</para>
/// </summary>
public class SolicitacaoProcedimento
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public Solicitacao? Solicitacao { get; set; }

    /// <summary>Posição no pedido (1 = principal, espelhado nos escalares da espinha). Na ordem em
    /// que o procedimento apareceu — não muda depois.</summary>
    public int Ordem { get; set; }

    /// <summary>O código interno (<c>pa</c>) do SISREG, coluna 1 da linha exportada. Vem em todas as
    /// linhas do export (1.016.979 conferidas em 16/10/2026); só falta em pedido que entrou por
    /// outro caminho.</summary>
    public string? ProcedimentoCodigoSisreg { get; set; }

    /// <summary>SIGTAP como veio (coluna 2). NÃO identifica o item: os dois joelhos têm o mesmo.</summary>
    public string? ProcedimentoSigtapCodigo { get; set; }

    /// <summary>Nome do procedimento no SISREG (coluna 3), normalizado como na espinha.</summary>
    public string? ProcedimentoTexto { get; set; }

    public string? ProfissionalExecutanteCpf { get; set; }
    public string? ProfissionalExecutanteNome { get; set; }

    /// <summary>Instante agendado deste item (UTC).</summary>
    public DateTime? DataAgendada { get; set; }

    /// <summary>Linha crua do TXT que trouxe este item (proveniência). Uso interno.</summary>
    public string? RawSisreg { get; set; }

    // ---- Auditoria ADR-0006 ----

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
