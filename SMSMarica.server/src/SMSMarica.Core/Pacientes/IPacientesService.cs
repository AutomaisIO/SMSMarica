using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes;

public interface IPacientesService
{
    /// <summary>
    /// Busca em tempo real por nome (qualquer parte, múltiplos tokens) ou CPF
    /// (formatado ou não). Sem termo retorna os 10 últimos cadastros ativos
    /// (ordenados por CriadoEm desc). Com termo, limita a 10 ocorrências mais
    /// relevantes. Soft-deleted não aparecem.
    /// </summary>
    Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo,
        CancellationToken cancellationToken = default);

    Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna informação mínima para verificação de existência por CPF, incluindo
    /// registros desativados. Usado pelo fluxo de cadastro para oferecer reativação.
    /// </summary>
    Task<PacienteExistenciaDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica existência por CNS (Patient.identifier do CNS = 15 dígitos). Retorna o
    /// primeiro match ou null. Usado pela importação SISREG para reusar o paciente quando
    /// o CADSUS não devolve o CPF (o cidadão já pode existir no hub sob o CNS).
    /// </summary>
    Task<PacienteExistenciaDto?> ObterPorCnsAsync(string cns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca um paciente pelo número de telefone (Patient.telecom). Retorna o
    /// primeiro match (id, nome, cpf) ou null. Usado pelo agente de voz para
    /// reconhecer quem liga de um número já cadastrado.
    /// </summary>
    Task<PacienteExistenciaDto?> ObterPorTelefoneAsync(string telefone, CancellationToken cancellationToken = default);

    /// <summary>
    /// TODOS os pacientes que têm este telefone (Patient.telecom) — um celular de família
    /// costuma estar no cadastro da mãe, do filho e do avô. Quem atende precisa ver a lista
    /// inteira em vez de um match escolhido em silêncio.
    /// </summary>
    Task<IReadOnlyList<PacienteListItemDto>> ListarPorTelefoneAsync(
        string telefone, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promove um <see cref="SMSMarica.Data.Entities.Usuario"/> existente
    /// (sem papel atual) a Paciente, criando linha em paciente com os campos
    /// específicos. Papel é determinado pela existência da linha 1:1 (ADR-0006).
    /// </summary>
    Task<Guid> PromoverAsync(PromoverPacienteRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrige SÓ o nome oficial do paciente (fluxo "Verificar nome"), preservando
    /// o restante do cadastro. Endpoint dedicado porque <see cref="AtualizarAsync"/>
    /// trata o nome como imutável. A alteração fica registrada na trilha de
    /// auditoria (nome anterior → novo, usuário, data). No-op se o nome não muda.
    /// </summary>
    Task AtualizarNomeAsync(Guid id, AtualizarNomePacienteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carimba o CPF num paciente que entrou <b>sem ele</b> (importação do SISREG ancorada só no
    /// CNS). Endpoint dedicado porque <see cref="AtualizarAsync"/> trata o CPF como imutável — e
    /// ele é: sobrescrever o CPF de alguém é trocar a identidade da pessoa.
    ///
    /// <para>Recusa se o paciente já tem CPF diferente (<c>ConflitoException</c>) e se o CPF não
    /// passa no dígito verificador. Idempotente quando o CPF já é o mesmo.</para>
    ///
    /// <para><b>Não</b> verifica se o CPF já pertence a outro cadastro — essa decisão é de quem
    /// chama, porque a resposta certa depende do contexto (na recepção, é repontar a solicitação
    /// para o cadastro que já existe).</para>
    /// </summary>
    Task DefinirCpfAsync(Guid id, string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Traz para <paramref name="destinoId"/> os identificadores de um cadastro-sombra (aquele que
    /// a importação criou sem CPF) quando a recepção descobre que a pessoa já existia.
    ///
    /// <para><b>Por que o CNS TEM de vir junto:</b> sem isso a próxima varredura encontra o
    /// cadastro-sombra outra vez por CNS e a solicitação é repontada de novo — para sempre. Mover o
    /// CNS é o que fecha o ciclo.</para>
    ///
    /// <para>Só ACUMULA: o telefone entra por append e nenhum dado do destino é sobrescrito
    /// (regra "contato só acumula" — merge nenhum apaga telefone).</para>
    /// </summary>
    Task AbsorverIdentificadoresAsync(
        Guid destinoId, string? cns, string? telefone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona um telefone aos contatos do paciente (append em
    /// <c>Patient.telecom</c> nativo), sem substituir os existentes. Idempotente:
    /// se o número já constar, é no-op.
    /// </summary>
    Task AdicionarTelefoneAsync(Guid id, AdicionarTelefoneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza SÓ a foto do paciente, preservando todo o resto (carrega o estado atual
    /// e regrava só o campo). <c>null</c> remove a foto. Usado pelo app do cidadão.
    /// </summary>
    Task AtualizarFotoAsync(Guid id, string? fotoBase64, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza SÓ os contatos (e-mail + telefones) do paciente, preservando o resto.
    /// Usado pelo app do cidadão para o próprio cadastro.
    /// </summary>
    Task AtualizarContatoAsync(
        Guid id, string? email, string? telefonePrincipal, string? telefoneCelular,
        string? telefoneResidencial, CancellationToken cancellationToken = default);

    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Resumo para checar existência por CPF (inclusive inativos).</summary>
public sealed record PacienteExistenciaDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    bool Ativo);
