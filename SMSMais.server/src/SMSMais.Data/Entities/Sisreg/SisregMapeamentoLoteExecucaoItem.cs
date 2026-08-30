namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// O que aconteceu com UMA unidade dentro de um "sincroniza tudo"
/// (<see cref="SisregMapeamentoLoteExecucao"/>).
///
/// <para>Existe pela mesma razão de <see cref="SisregVarreduraExecucaoItem"/>: o agregado responde
/// "quanto no total", e a pergunta do operador é "quantos médicos vieram de cada unidade". Gravado
/// ao terminar cada unidade — então um lote interrompido preserva o detalhe do que já rodou.</para>
/// </summary>
public class SisregMapeamentoLoteExecucaoItem
{
    public Guid Id { get; set; }

    public Guid ExecucaoId { get; set; }

    public Guid UnidadeId { get; set; }

    /// <summary>Nome no momento da execução — desnormalizado para o rastreio sobreviver à edição
    /// ou exclusão do cadastro.</summary>
    public string UnidadeNome { get; set; } = string.Empty;

    public string? Cnes { get; set; }

    /// <summary>A unidade foi criada aqui nesta execução (veio do SISREG e não existia).</summary>
    public bool UnidadeCriada { get; set; }

    public ResultadoUnidadeLote Resultado { get; set; } = ResultadoUnidadeLote.Mapeada;

    public int ProfissionaisEncontrados { get; set; }
    public int ProfissionaisNovos { get; set; }
    public int ProfissionaisAusentes { get; set; }
    public int ProcedimentosEncontrados { get; set; }
    public int ProcedimentosNovos { get; set; }

    public int PractitionersCriados { get; set; }
    public int PractitionersVinculados { get; set; }

    public int Requisicoes { get; set; }

    /// <summary>Motivo quando <see cref="Resultado"/> não é <c>Mapeada</c> — o "por que pulou" ou o
    /// erro da unidade. Fica visível na tela: "Pulada" sozinho não diz nada.</summary>
    public string? Observacao { get; set; }

    public DateTime RegistradoEm { get; set; }

    public SisregMapeamentoLoteExecucao? Execucao { get; set; }
}

/// <summary>Desfecho de uma unidade dentro do lote.</summary>
public enum ResultadoUnidadeLote
{
    /// <summary>Foi ao SISREG e reconciliou o mapeamento.</summary>
    Mapeada = 1,

    /// <summary>Mapeamento ainda dentro do TTL — não foi ao SISREG de propósito.</summary>
    PuladaPorTtl = 2,

    /// <summary>Acabou o orçamento de requisições da janela; entra na próxima rodada (as mais
    /// antigas vêm primeiro, então ela sobe na fila sozinha).</summary>
    PuladaPorOrcamento = 3,

    /// <summary>Falhou — a mensagem está na observação. Uma unidade ruim não derruba o lote.</summary>
    Erro = 4,

    /// <summary>Só descoberta: unidade criada/atualizada no cadastro, sem ir ao mapeamento nesta
    /// execução.</summary>
    SomenteDescoberta = 5,
}
