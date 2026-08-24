using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Conversas;

/// <summary>
/// Mensagem pronta ("resposta rápida") do chat: o texto que o operador manda o tempo todo,
/// guardado uma vez em vez de redigitado. O corpo aceita tags <c>{{nome}}</c>: as
/// AUTOMÁTICAS saem do contexto da conversa (paciente, operador, unidade) e as MANUAIS são
/// declaradas em <see cref="Campos"/> com um tipo (texto/data/número) e preenchidas na hora.
///
/// Não confundir com template da Meta: aquele é aprovado pela Meta e serve para ABRIR
/// conversa; este é texto livre, só vale DENTRO da janela de 24h e nunca sai daqui sem
/// passar pelo campo de digitação do operador.
/// </summary>
public class RespostaRapida
{
    public Guid Id { get; set; }

    /// <summary>Como o atalho aparece na lista lateral do chat.</summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>Texto com as tags <c>{{...}}</c>.</summary>
    public string Corpo { get; set; } = string.Empty;

    /// <summary>Agrupador livre para a lista não virar um paredão ("Agendamento", "Exames").</summary>
    public string? Categoria { get; set; }

    /// <summary><c>null</c> = vale para toda a SMS; preenchido = só a unidade dona a enxerga.</summary>
    public Guid? UnidadeId { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>Ordem na lista (menor primeiro); empate desempata por título.</summary>
    public int Ordem { get; set; }

    public ICollection<RespostaRapidaCampo> Campos { get; set; } = [];

    public Unidade? Unidade { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}

/// <summary>
/// Uma variável MANUAL de uma <see cref="RespostaRapida"/> — o que o operador preenche ao
/// usar o atalho. O <see cref="Nome"/> é o que aparece no corpo como <c>{{nome}}</c>.
/// </summary>
public class RespostaRapidaCampo
{
    public Guid Id { get; set; }

    public Guid RespostaRapidaId { get; set; }

    /// <summary>Chave usada no corpo (minúsculas, sem espaço) — ex.: <c>data_exame</c>.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Rótulo do campo no mini-formulário. Sem isso, mostramos o próprio nome.</summary>
    public string? Rotulo { get; set; }

    public TipoCampoRespostaRapida Tipo { get; set; } = TipoCampoRespostaRapida.Texto;

    public int Ordem { get; set; }

    public RespostaRapida? RespostaRapida { get; set; }
}
