namespace SMSMais.Core.Pacientes.Dtos;

/// <summary>
/// Pedido de unificação de dois cadastros do MESMO paciente. O operador escolhe quem
/// <b>fica</b> (<see cref="SobreviventeId"/>) e, na tela, resolve campo a campo qual valor
/// predomina — o resultado dessa resolução chega em <see cref="DadosFinais"/>/<see cref="NomeFinal"/>
/// e é aplicado ao sobrevivente antes da fusão.
/// </summary>
public sealed record UnificarPacientesRequest(
    Guid SobreviventeId,
    Guid AbsorvidoId,
    /// <summary>
    /// Demografia final escolhida na tela (os valores que "ficam"). Aplicada ao sobrevivente pelo
    /// caminho de edição normal — portanto auditada campo a campo. Nulo = mantém o sobrevivente
    /// como está. Nome, CNS e nascimento não entram aqui (vêm em <see cref="NomeFinal"/>,
    /// <see cref="CnsFinal"/> e <see cref="NascimentoFinal"/>). O CPF não se escolhe: o cadastro
    /// que fica é o dono do CPF escolhido na tela. Telefones e e-mail também não: os do absorvido
    /// são SOMADOS ao sobrevivente (contato só acumula).
    /// </summary>
    AtualizarPacienteRequest? DadosFinais = null,
    /// <summary>Nome oficial final escolhido, quando o operador decidir trocar o do sobrevivente.</summary>
    string? NomeFinal = null,
    /// <summary>
    /// Confirmação explícita de que, mesmo com CPF e/ou CNS DIFERENTES entre os dois cadastros,
    /// é a mesma pessoa. Sem isso a unificação é recusada no caso divergente (risco de fundir
    /// duas pessoas distintas — ADR-0041).
    /// </summary>
    bool ConfirmaChavesDivergentes = false,
    /// <summary>
    /// CNS que vira o oficial do cadastro unificado. Tem de ser o CNS de um dos dois cadastros; o
    /// outro continua guardado como antigo (segue achando a pessoa). Nulo = mantém o do sobrevivente.
    /// </summary>
    string? CnsFinal = null,
    /// <summary>
    /// Data de nascimento do cadastro unificado. Tem de ser a de um dos dois cadastros. Nulo =
    /// mantém a do sobrevivente.
    /// </summary>
    DateOnly? NascimentoFinal = null,
    /// <summary>
    /// Telefones (dígitos) que o operador DESLIGOU na tela. O do absorvido não é importado; o do
    /// sobrevivente é aposentado (vira histórico). O verificado nunca sai, mesmo que venha aqui.
    /// Nulo/vazio = todos os telefones dos dois ficam (contato acumula).
    /// </summary>
    IReadOnlyList<string>? TelefonesDescartados = null);

/// <summary>Prévia (dry-run) da unificação: os dois cadastros, as divergências e o que será movido.</summary>
public sealed record PreviaUnificacaoDto(
    PacienteDto Sobrevivente,
    PacienteDto Absorvido,
    /// <summary>CPF preenchido nos dois e diferente — caso perigoso, pede confirmação.</summary>
    bool CpfDivergente,
    /// <summary>CNS preenchido nos dois e diferente — caso perigoso, pede confirmação.</summary>
    bool CnsDivergente,
    /// <summary>Campos cujo valor difere entre os dois cadastros (para a resolução na tela).</summary>
    IReadOnlyList<DivergenciaCampoDto> Campos,
    /// <summary>Quantas linhas de cada módulo apontam para o absorvido e serão repontadas.</summary>
    IReadOnlyList<ContagemModuloDto> Referencias,
    int TotalReferencias);

/// <summary>Um campo divergente entre os dois cadastros.</summary>
public sealed record DivergenciaCampoDto(string Campo, string? Sobrevivente, string? Absorvido);

/// <summary>Quantas referências ao paciente um módulo do painel tem (agregado para exibição).</summary>
public sealed record ContagemModuloDto(string Modulo, int Quantidade);

/// <summary>Resultado da unificação concluída — o que foi efetivamente movido.</summary>
public sealed record ResultadoUnificacaoDto(
    Guid SobreviventeId,
    Guid AbsorvidoId,
    int ReferenciasRepontadas,
    int ClinicoRepontado,
    int IdentificadoresAbsorvidos,
    /// <summary>Telefones/e-mails do absorvido que entraram no sobrevivente.</summary>
    int ContatosSomados = 0,
    /// <summary>Telefones do sobrevivente que o operador desligou e foram aposentados.</summary>
    int TelefonesRetirados = 0);
