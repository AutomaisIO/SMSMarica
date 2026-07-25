namespace SMSMarica.Data.Entities.Sisreg;

/// <summary>
/// Procedimento que um profissional executa numa unidade, como cadastrado no SISREG.
/// Alimentado pelo AJAX <c>PROCEDIMENTOS_POR_PROFISSIONAIS_E_UPS</c>.
///
/// <para>Cada par (profissional, procedimento) habilitado vira <b>uma consulta</b> na varredura
/// da agenda — por isso o <see cref="Habilitado"/> é granular aqui e não só no profissional.</para>
///
/// <para><b>Nuance dos grupos:</b> procedimentos cujo código termina em <c>000</c> são
/// "GRUPO - X" e a consulta deles devolve também os itens individuais. Habilitar o grupo e os
/// itens ao mesmo tempo duplica trabalho — a varredura deduplica por código de solicitação,
/// mas gasta requisição à toa.</para>
/// </summary>
public class SisregProcedimentoProfissional
{
    public Guid Id { get; set; }

    public Guid ProfissionalId { get; set; }

    /// <summary>Código do procedimento no SISREG (7 dígitos, ex.: <c>1305007</c>).</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Descrição como o SISREG a exibe (ex.: <c>MAMOGRAFIA BILATERAL</c>).</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Entra na varredura de agenda.</summary>
    public bool Habilitado { get; set; }

    /// <summary>Código terminado em <c>000</c>: a consulta traz também os itens individuais.</summary>
    public bool Grupo { get; set; }

    /// <summary>Última vez que o SISREG confirmou este procedimento para o profissional.</summary>
    public DateTime VistoEm { get; set; }

    /// <summary>Sumiu da lista do SISREG na última atualização. Mantido para preservar a habilitação.</summary>
    public bool Ausente { get; set; }

    public SisregProfissionalUnidade? Profissional { get; set; }
}
