using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Pacientes;

public sealed class PacientesService(SmsMaricaDbContext db, IUsuarioAtualAccessor atual) : IPacientesService
{
    private const int LimiteBusca = 10;
    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";
    private readonly SmsMaricaDbContext _db = db;
    private readonly IUsuarioAtualAccessor _atual = atual;

    public async Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Paciente> query = _db.Pacientes.AsNoTracking()
            .Include(p => p.Usuario)
            .Where(p => p.ExcluidoEm == null);

        // Sem termo: últimos cadastrados, ordenados do mais novo para o mais antigo.
        if (string.IsNullOrWhiteSpace(termo))
        {
            var recentes = await query
                .OrderByDescending(p => p.CriadoEm)
                .Take(LimiteBusca)
                .ToListAsync(cancellationToken);
            return [.. recentes.Select(PacientesMapper.ParaListItem)];
        }

        termo = termo.Trim();
        var digitos = NormalizarDigitos(termo);

        // Se o usuário digitou apenas dígitos (ou majoritariamente), tenta CPF.
        if (digitos.Length >= 3 && digitos.Length <= 11 && digitos.Length == termo.Replace(".", "").Replace("-", "").Replace(" ", "").Length)
        {
            query = query.Where(p => p.Usuario.Cpf != null
                && (EF.Functions.Like(p.Usuario.Cpf, digitos + "%") || EF.Functions.Like(p.Usuario.Cpf, "%" + digitos + "%")));
        }
        else
        {
            // Tokenização por espaço — cada token precisa aparecer em alguma parte do nome (ILIKE).
            var tokens = termo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var like = "%" + token + "%";
                query = query.Where(p => EF.Functions.ILike(p.Usuario.NomeCompleto, like));
            }
        }

        var encontrados = await query
            .OrderBy(p => p.Usuario.NomeCompleto)
            .Take(LimiteBusca)
            .ToListAsync(cancellationToken);

        return [.. encontrados.Select(PacientesMapper.ParaListItem)];
    }

    public async Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .AsNoTracking()
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        return PacientesMapper.ParaDto(paciente);
    }

    public async Task<PacienteExistenciaDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var normalizado = NormalizarDigitos(cpf);
        if (normalizado.Length != 11) return null;

        var paciente = await _db.Pacientes
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Where(p => p.Usuario.Cpf == normalizado)
            .Select(p => new PacienteExistenciaDto(p.Id, p.Usuario.NomeCompleto, p.Usuario.Cpf!, p.ExcluidoEm == null))
            .FirstOrDefaultAsync(cancellationToken);

        return paciente;
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = NormalizarDigitos(request.Cpf);

        var usuarioExistente = await _db.Usuarios.AsNoTracking()
            .Include(u => u.Paciente)
            .Where(u => u.Cpf == cpfNormalizado)
            .FirstOrDefaultAsync(cancellationToken);

        if (usuarioExistente is not null)
        {
            if (usuarioExistente.Paciente is not null)
            {
                if (usuarioExistente.Paciente.ExcluidoEm is null)
                {
                    throw new ConflitoException("paciente.cpf_duplicado", "Já existe paciente ativo com este CPF.");
                }
                throw new ConflitoException("paciente.cpf_excluido", "Existe paciente excluído com este CPF. Reative o cadastro.");
            }

            throw new ConflitoException(
                "paciente.cpf_ja_cadastrado",
                $"CPF já cadastrado como usuário '{usuarioExistente.NomeCompleto}'. Use /pacientes/promover.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Cpf = cpfNormalizado,
            Rg = NormalizaOpcional(request.Rg, false),
            DataNascimento = request.DataNascimento,
            Sexo = request.Sexo,
            Email = NormalizarEmail(request.Email, cpfNormalizado),
            Telefone = NormalizaOpcional(request.TelefonePrincipal, false),
            Endereco = request.Endereco?.ParaEntidade(),
            FotoBase64 = NormalizaOpcional(request.FotoBase64, false),
            SenhaHash = SenhaHashPlaceholder,
            DeveTrocarSenha = true,
            Ativo = true,
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        var paciente = new Paciente
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            NomeSocial = NormalizaOpcional(request.NomeSocial, false),
            Cns = NormalizaOpcional(request.Cns, true),
            EstadoCivil = request.EstadoCivil,
            RacaCor = request.RacaCor,
            Escolaridade = request.Escolaridade,
            Ocupacao = NormalizaOpcional(request.Ocupacao, false),
            Naturalidade = NormalizaOpcional(request.Naturalidade, false),
            Nacionalidade = string.IsNullOrWhiteSpace(request.Nacionalidade) ? "Brasileira" : request.Nacionalidade!.Trim(),
            NomeDaMae = NormalizaOpcional(request.NomeDaMae, false),
            NomeDoPai = NormalizaOpcional(request.NomeDoPai, false),
            ResponsavelLegal = NormalizaOpcional(request.ResponsavelLegal, false),
            TelefoneCelular = NormalizaOpcional(request.TelefoneCelular, false),
            TelefoneResidencial = NormalizaOpcional(request.TelefoneResidencial, false),
            ContatoEmergencia = request.ContatoEmergencia is null ? null : PacientesMapper.ParaEntidade(request.ContatoEmergencia),
            AlturaCm = request.AlturaCm,
            PesoKg = request.PesoKg,
            TipoSanguineo = request.TipoSanguineo,
            FatorRh = request.FatorRh,
            Alergias = SanearLista(request.Alergias),
            MedicamentosContinuos = SanearLista(request.MedicamentosContinuos),
            Comorbidades = SanearLista(request.Comorbidades),
            Deficiencias = SanearLista(request.Deficiencias),
            PlanoSaude = NormalizaOpcional(request.PlanoSaude, false),
            Observacoes = NormalizaOpcional(request.Observacoes, false),
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        _db.Usuarios.Add(usuario);
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);

        return paciente.Id;
    }

    public async Task<Guid> PromoverAsync(PromoverPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Medico)
            .Include(u => u.Motorista)
            .Include(u => u.Paciente)
            .FirstOrDefaultAsync(u => u.Id == request.UsuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), request.UsuarioId);

        var papelAtual = usuario.Medico is not null ? "Medico"
                       : usuario.Motorista is not null ? "Motorista"
                       : usuario.Paciente is not null ? "Paciente"
                       : null;
        if (papelAtual is not null)
        {
            throw new ConflitoException(
                "paciente.usuario_ja_tem_papel",
                $"Usuário já tem papel '{papelAtual}'. Elimine o papel atual antes de promover.");
        }

        if (string.IsNullOrWhiteSpace(usuario.Cpf))
        {
            throw new ConflitoException(
                "paciente.usuario_sem_cpf",
                "Usuário precisa ter CPF cadastrado para virar paciente.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;
        var paciente = new Paciente
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            NomeSocial = NormalizaOpcional(request.NomeSocial, false),
            Cns = NormalizaOpcional(request.Cns, true),
            EstadoCivil = request.EstadoCivil,
            RacaCor = request.RacaCor,
            Escolaridade = request.Escolaridade,
            Ocupacao = NormalizaOpcional(request.Ocupacao, false),
            Naturalidade = NormalizaOpcional(request.Naturalidade, false),
            Nacionalidade = string.IsNullOrWhiteSpace(request.Nacionalidade) ? "Brasileira" : request.Nacionalidade!.Trim(),
            NomeDaMae = NormalizaOpcional(request.NomeDaMae, false),
            NomeDoPai = NormalizaOpcional(request.NomeDoPai, false),
            ResponsavelLegal = NormalizaOpcional(request.ResponsavelLegal, false),
            TelefoneCelular = NormalizaOpcional(request.TelefoneCelular, false),
            TelefoneResidencial = NormalizaOpcional(request.TelefoneResidencial, false),
            ContatoEmergencia = request.ContatoEmergencia is null ? null : PacientesMapper.ParaEntidade(request.ContatoEmergencia),
            AlturaCm = request.AlturaCm,
            PesoKg = request.PesoKg,
            TipoSanguineo = request.TipoSanguineo,
            FatorRh = request.FatorRh,
            Alergias = SanearLista(request.Alergias),
            MedicamentosContinuos = SanearLista(request.MedicamentosContinuos),
            Comorbidades = SanearLista(request.Comorbidades),
            Deficiencias = SanearLista(request.Deficiencias),
            PlanoSaude = NormalizaOpcional(request.PlanoSaude, false),
            Observacoes = NormalizaOpcional(request.Observacoes, false),
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);
        return paciente.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        if (paciente.ExcluidoEm is not null)
        {
            throw new ConflitoException("paciente.excluido", "Paciente excluído não pode ser editado.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        // Atualizar Usuario (dados pessoais base) — nome e CPF são imutáveis.
        paciente.Usuario.Rg = NormalizaOpcional(request.Rg, false);
        paciente.Usuario.Sexo = request.Sexo;
        paciente.Usuario.Endereco = request.Endereco?.ParaEntidade();
        paciente.Usuario.Telefone = NormalizaOpcional(request.TelefonePrincipal, false);
        paciente.Usuario.FotoBase64 = NormalizaOpcional(request.FotoBase64, false);
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            paciente.Usuario.Email = request.Email.Trim().ToLowerInvariant();
        }
        paciente.Usuario.AtualizadoEm = agora;
        paciente.Usuario.AtualizadoPor = atualId;

        // Atualizar Paciente (específicos)
        paciente.NomeSocial = NormalizaOpcional(request.NomeSocial, false);
        paciente.Cns = NormalizaOpcional(request.Cns, true);
        paciente.EstadoCivil = request.EstadoCivil;
        paciente.RacaCor = request.RacaCor;
        paciente.Escolaridade = request.Escolaridade;
        paciente.Ocupacao = NormalizaOpcional(request.Ocupacao, false);
        paciente.Naturalidade = NormalizaOpcional(request.Naturalidade, false);
        paciente.Nacionalidade = string.IsNullOrWhiteSpace(request.Nacionalidade) ? "Brasileira" : request.Nacionalidade!.Trim();
        paciente.NomeDaMae = NormalizaOpcional(request.NomeDaMae, false);
        paciente.NomeDoPai = NormalizaOpcional(request.NomeDoPai, false);
        paciente.ResponsavelLegal = NormalizaOpcional(request.ResponsavelLegal, false);
        paciente.TelefoneCelular = NormalizaOpcional(request.TelefoneCelular, false);
        paciente.TelefoneResidencial = NormalizaOpcional(request.TelefoneResidencial, false);
        paciente.ContatoEmergencia = request.ContatoEmergencia is null ? null : PacientesMapper.ParaEntidade(request.ContatoEmergencia);
        paciente.AlturaCm = request.AlturaCm;
        paciente.PesoKg = request.PesoKg;
        paciente.TipoSanguineo = request.TipoSanguineo;
        paciente.FatorRh = request.FatorRh;
        paciente.Alergias = SanearLista(request.Alergias);
        paciente.MedicamentosContinuos = SanearLista(request.MedicamentosContinuos);
        paciente.Comorbidades = SanearLista(request.Comorbidades);
        paciente.Deficiencias = SanearLista(request.Deficiencias);
        paciente.PlanoSaude = NormalizaOpcional(request.PlanoSaude, false);
        paciente.Observacoes = NormalizaOpcional(request.Observacoes, false);
        paciente.AtualizadoEm = agora;
        paciente.AtualizadoPor = atualId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        if (paciente.ExcluidoEm is not null)
        {
            throw new ConflitoException("paciente.ja_excluido", "Paciente já foi excluído.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        paciente.ExcluidoEm = agora;
        paciente.ExcluidoPor = atualId;
        paciente.Usuario.ExcluidoEm = agora;
        paciente.Usuario.ExcluidoPor = atualId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        if (paciente.ExcluidoEm is null)
        {
            throw new ConflitoException("paciente.nao_excluido", "Paciente não está excluído.");
        }

        paciente.ExcluidoEm = null;
        paciente.ExcluidoPor = null;
        paciente.Usuario.ExcluidoEm = null;
        paciente.Usuario.ExcluidoPor = null;
        paciente.AtualizadoEm = DateTime.UtcNow;
        paciente.AtualizadoPor = _atual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizarDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);

    private static string? NormalizaOpcional(string? valor, bool soDigitos)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        return soDigitos ? NormalizarDigitos(v) : v;
    }

    private static List<string> SanearLista(IReadOnlyList<string>? lista)
    {
        if (lista is null || lista.Count == 0) return [];
        return [.. lista
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)];
    }

    private static string NormalizarEmail(string? email, string cpf) =>
        string.IsNullOrWhiteSpace(email)
            ? $"paciente-{cpf}@local.smsmarica"
            : email.Trim().ToLowerInvariant();
}
