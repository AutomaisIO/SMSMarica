using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Motoristas;

/// <summary>
/// Serviço de motoristas. Após Fatia 4 do refator FHIR, <see cref="Motorista"/>
/// carrega identidade inline (motorista não é entidade clínica FHIR), e o
/// vínculo de login fica em <c>Usuario.MotoristaId</c>.
/// </summary>
public sealed class MotoristasService(SmsMaricaDbContext db, IUsuarioAtualAccessor atual) : IMotoristasService
{
    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";
    private readonly SmsMaricaDbContext _db = db;
    private readonly IUsuarioAtualAccessor _atual = atual;

    public async Task<IReadOnlyList<MotoristaListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var motoristas = await _db.Motoristas.AsNoTracking()
            .Where(m => m.ExcluidoEm == null)
            .OrderBy(m => m.NomeCompleto)
            .ToListAsync(cancellationToken);

        var usuariosPorMotorista = await CarregarUsuariosAsync(
            motoristas.Select(m => m.Id).ToList(), cancellationToken);

        return [.. motoristas.Select(m => MotoristasMapper.ParaListItem(
            m, usuariosPorMotorista.GetValueOrDefault(m.Id)))];
    }

    public async Task<MotoristaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var motorista = await _db.Motoristas.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        var usuario = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.MotoristaId == motorista.Id, cancellationToken);

        return MotoristasMapper.ParaDto(motorista, usuario);
    }

    public async Task<Guid> CadastrarAsync(CadastrarMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = NormalizarDigitos(request.Cpf);
        var cnh = request.Cnh.Trim();

        if (await _db.Motoristas.AsNoTracking().AnyAsync(m => m.Cpf == cpf && m.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("motorista.cpf_duplicado", "Já existe motorista com este CPF.");
        }

        if (await _db.Motoristas.AsNoTracking().AnyAsync(m => m.Cnh == cnh && m.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        var email = NormalizarEmail(request.Email, cpf);
        if (await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflitoException("motorista.email_duplicado", "Já existe usuário com este e-mail.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        var motorista = new Motorista
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Cpf = cpf,
            DataNascimento = request.DataNascimento,
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Endereco = request.Endereco?.ParaEntidade(),
            FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64,
            Cnh = cnh,
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            SenhaHash = SenhaHashPlaceholder,
            DeveTrocarSenha = true,
            Ativo = true,
            NomeExibicao = motorista.NomeCompleto,
            MotoristaId = motorista.Id,
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        _db.Motoristas.Add(motorista);
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(cancellationToken);
        return motorista.Id;
    }

    public Task<Guid> PromoverAsync(PromoverMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        // Igual a Médico/Paciente — Usuario sem papel não carrega mais identidade
        // clínica, então "promover" perdeu o sentido. Use Cadastrar normal.
        throw new ConflitoException(
            "motorista.promover_indisponivel",
            "Promoção descontinuada — Usuario sem papel não carrega mais identidade. Use POST /motoristas.");
    }

    public async Task AtualizarAsync(Guid id, AtualizarMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var motorista = await _db.Motoristas.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        if (motorista.ExcluidoEm is not null)
        {
            throw new ConflitoException("motorista.excluido", "Motorista excluído não pode ser editado.");
        }

        var cnh = request.Cnh.Trim();
        if (cnh != motorista.Cnh &&
            await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh && x.Id != id && x.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        motorista.Cnh = cnh;
        motorista.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        motorista.Endereco = request.Endereco?.ParaEntidade();
        motorista.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;
        motorista.AtualizadoEm = agora;
        motorista.AtualizadoPor = atualId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var motorista = await _db.Motoristas.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        if (motorista.ExcluidoEm is not null)
        {
            throw new ConflitoException("motorista.ja_excluido", "Motorista já foi excluído.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        motorista.ExcluidoEm = agora;
        motorista.ExcluidoPor = atualId;

        // Cascateia para o Usuario vinculado.
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.MotoristaId == motorista.Id, cancellationToken);
        if (usuario is not null)
        {
            usuario.ExcluidoEm = agora;
            usuario.ExcluidoPor = atualId;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, Usuario>> CarregarUsuariosAsync(
        IReadOnlyList<Guid> motoristaIds, CancellationToken ct)
    {
        if (motoristaIds.Count == 0) return [];
        var usuarios = await _db.Usuarios.AsNoTracking()
            .Where(u => u.MotoristaId.HasValue && motoristaIds.Contains(u.MotoristaId.Value))
            .ToListAsync(ct);
        return usuarios.ToDictionary(u => u.MotoristaId!.Value);
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);

    private static string NormalizarEmail(string? email, string cpf) =>
        string.IsNullOrWhiteSpace(email)
            ? $"motorista-{cpf}@local.smsmarica"
            : email.Trim().ToLowerInvariant();
}
