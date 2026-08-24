using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.Telefones.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Telefones;

public sealed class DispensaContatoService(
    SmsMaisDbContext db,
    IPacienteFhirClient fhir,
    IUsuarioAtualAccessor atual,
    ILogger<DispensaContatoService> logger) : IDispensaContatoService
{
    /// <summary>Descrição mínima para "Outro" — evita o "x" que não explica nada na auditoria.</summary>
    private const int MinimoDescricaoOutro = 5;

    public IReadOnlyList<MotivoDispensaContatoDto> ListarMotivos() =>
        [.. DispensaContatoRegras.Todos.Select(m => new MotivoDispensaContatoDto(
            m,
            DispensaContatoRegras.Rotulo(m),
            DispensaContatoRegras.PermiteEnvio(m),
            DispensaContatoRegras.Consequencia(m),
            ExigeDescricao: m == MotivoDispensaContato.Outro))];

    public async Task<DispensaContatoDto> RegistrarAsync(
        Guid pacienteId, MotivoDispensaContato motivo, string? motivoDescricao, bool pacienteCiente,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(motivo))
            throw new ValidacaoException("dispensa.motivo_invalido", "Selecione um motivo válido para a dispensa.");

        // A ciência do paciente é o coração da dispensa: ele está abrindo mão de receber o
        // resultado no celular. Sem a afirmação do operador, não há o que registrar.
        if (!pacienteCiente)
            throw new ValidacaoException(
                "dispensa.sem_ciencia",
                "Confirme que o paciente foi informado de que não receberá avisos por WhatsApp e concordou.");

        var descricao = (motivoDescricao ?? string.Empty).Trim();
        if (motivo == MotivoDispensaContato.Outro && descricao.Length < MinimoDescricaoOutro)
            throw new ValidacaoException(
                "dispensa.descricao_obrigatoria", "Descreva o motivo da dispensa (mínimo 5 caracteres).");
        if (descricao.Length == 0) descricao = string.Empty;

        // Valida a existência do paciente no hub E aproveita para carimbar o telefone que estava
        // no cadastro na hora — auditoria de "dispensou com qual número em mãos".
        var patient = await fhir.ObterAsync(pacienteId, ct)
            ?? throw new NaoEncontradoException("Paciente", pacienteId);
        var telefoneNaEpoca = PacienteFhirMapper.ParaDto(patient).TelefonePrincipal;

        var agora = DateTime.UtcNow;
        var quem = atual.UsuarioId;

        // Append-only: a ativa anterior (se houver) é revogada, não editada — trocar o motivo
        // preserva o registro do motivo antigo.
        var anterior = await db.DispensasVerificacaoContato
            .FirstOrDefaultAsync(d => d.PacienteId == pacienteId && d.RevogadoEm == null, ct);
        if (anterior is not null)
        {
            anterior.RevogadoEm = agora;
            anterior.RevogadoPor = quem;
            anterior.RevogadoMotivo = "Substituída por uma nova dispensa";
        }

        var dispensa = new DispensaVerificacaoContato
        {
            Id = Guid.CreateVersion7(),
            PacienteId = pacienteId,
            Motivo = motivo,
            MotivoDescricao = descricao.Length == 0 ? null : descricao,
            PacienteCiente = true,
            TelefoneNaEpoca = telefoneNaEpoca,
            CriadoEm = agora,
            CriadoPor = quem,
        };
        db.DispensasVerificacaoContato.Add(dispensa);
        await db.SaveChangesAsync(ct);

        await AjustarComunicacoesRetidasAsync(pacienteId, motivo, agora, ct);

        logger.LogInformation(
            "Dispensa de verificação de contato registrada para o paciente {Paciente}: {Motivo} " +
            "(envio por WhatsApp {Envio}; operador {Usuario}).",
            pacienteId, motivo, DispensaContatoRegras.PermiteEnvio(motivo) ? "liberado" : "bloqueado", quem);

        return ParaDto(dispensa, criadoPorNome: null);
    }

    public async Task RevogarAsync(Guid pacienteId, string? motivo, CancellationToken ct = default)
    {
        var ativa = await db.DispensasVerificacaoContato
            .FirstOrDefaultAsync(d => d.PacienteId == pacienteId && d.RevogadoEm == null, ct);
        if (ativa is null) return;

        ativa.RevogadoEm = DateTime.UtcNow;
        ativa.RevogadoPor = atual.UsuarioId;
        ativa.RevogadoMotivo = string.IsNullOrWhiteSpace(motivo) ? "Revogada no painel" : motivo.Trim();
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Dispensa de verificação de contato do paciente {Paciente} revogada: {Motivo}.",
            pacienteId, ativa.RevogadoMotivo);
    }

    public async Task<DispensaContatoDto?> ObterAtivaAsync(Guid pacienteId, CancellationToken ct = default)
    {
        if (pacienteId == Guid.Empty) return null;
        var d = await db.DispensasVerificacaoContato.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PacienteId == pacienteId && x.RevogadoEm == null, ct);
        if (d is null) return null;

        var nome = d.CriadoPor is { } por
            ? await db.Usuarios.AsNoTracking().Where(u => u.Id == por)
                .Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct)
            : null;
        return ParaDto(d, nome);
    }

    public async Task<IReadOnlyDictionary<Guid, DispensaContatoDto>> ObterAtivasAsync(
        IEnumerable<Guid> pacienteIds, CancellationToken ct = default)
    {
        var ids = pacienteIds.Where(i => i != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, DispensaContatoDto>();

        var ativas = await db.DispensasVerificacaoContato.AsNoTracking()
            .Where(d => ids.Contains(d.PacienteId) && d.RevogadoEm == null)
            .ToListAsync(ct);

        // Indexador (não ToDictionary): o índice único filtrado garante uma ativa por paciente,
        // mas uma duplicata legada não pode derrubar a listagem inteira com ArgumentException.
        var mapa = new Dictionary<Guid, DispensaContatoDto>(ativas.Count);
        foreach (var d in ativas) mapa[d.PacienteId] = ParaDto(d, criadoPorNome: null);
        return mapa;
    }

    /// <summary>
    /// Acerta as comunicações que o worker havia retido por falta de contato verificado
    /// (<see cref="StatusComunicacao.AguardandoTelefoneVerificado"/>).
    ///
    /// Motivo COM canal → solta a fila com <c>IgnorarVerificacaoTelefone</c>, e a decisão fica
    /// persistida para as retentativas honrarem. Motivo SEM canal → a comunicação continua
    /// retida (não é terminal: verificar o telefone depois ainda a solta), mas o motivo exibido
    /// passa a dizer a verdade — "entrega presencial" em vez de "aguardando verificação", que
    /// fazia a equipe ficar esperando algo que nunca ia acontecer.
    /// </summary>
    private async Task AjustarComunicacoesRetidasAsync(
        Guid pacienteId, MotivoDispensaContato motivo, DateTime agora, CancellationToken ct)
    {
        var retidas = db.ComunicacoesPaciente
            .Where(c => c.PacienteId == pacienteId
                        && c.Status == StatusComunicacao.AguardandoTelefoneVerificado);

        int afetadas;
        if (DispensaContatoRegras.PermiteEnvio(motivo))
        {
            afetadas = await retidas.ExecuteUpdateAsync(set => set
                .SetProperty(c => c.Status, StatusComunicacao.Pendente)
                .SetProperty(c => c.IgnorarVerificacaoTelefone, true)
                .SetProperty(c => c.MotivoFalha, (string?)null)
                .SetProperty(c => c.ProximaTentativaEm, agora), ct);
        }
        else
        {
            afetadas = await retidas.ExecuteUpdateAsync(set => set
                .SetProperty(c => c.MotivoFalha,
                    "Verificação dispensada (" + DispensaContatoRegras.Rotulo(motivo) +
                    ") — resultado e laudo devem ser entregues presencialmente."), ct);
        }

        if (afetadas > 0)
            logger.LogInformation(
                "Dispensa do paciente {Paciente}: {N} comunicação(ões) retida(s) {Acao}.",
                pacienteId, afetadas, DispensaContatoRegras.PermiteEnvio(motivo) ? "liberada(s)" : "reetiquetada(s)");
    }

    private static DispensaContatoDto ParaDto(DispensaVerificacaoContato d, string? criadoPorNome) =>
        new(d.Id, d.PacienteId, d.Motivo,
            DispensaContatoRegras.Rotulo(d.Motivo),
            d.MotivoDescricao,
            // "Outro" só diz alguma coisa pela descrição; nos demais o rótulo é o texto útil.
            MotivoTexto: d.Motivo == MotivoDispensaContato.Outro && !string.IsNullOrWhiteSpace(d.MotivoDescricao)
                ? d.MotivoDescricao!
                : DispensaContatoRegras.Rotulo(d.Motivo),
            PermiteEnvio: DispensaContatoRegras.PermiteEnvio(d.Motivo),
            d.CriadoEm, d.CriadoPor, criadoPorNome);
}
