using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes;

public sealed class PacientesService(SmsMaricaDbContext db) : IPacientesService
{
    private const int LimiteBusca = 10;
    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Paciente> query = _db.Pacientes.AsNoTracking()
            .Include(p => p.Usuario)
            .Where(p => p.Ativo);

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
            .Select(p => new PacienteExistenciaDto(p.Id, p.Usuario.NomeCompleto, p.Usuario.Cpf!, p.Ativo))
            .FirstOrDefaultAsync(cancellationToken);

        return paciente;
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = NormalizarDigitos(request.Cpf);

        var usuarioExistente = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Cpf == cpfNormalizado)
            .Select(u => new { u.Id, u.NomeCompleto, u.TipoPapel })
            .FirstOrDefaultAsync(cancellationToken);

        if (usuarioExistente is not null)
        {
            if (usuarioExistente.TipoPapel == TipoPapel.Paciente)
            {
                var pacienteExistente = await _db.Pacientes.AsNoTracking()
                    .Where(p => p.UsuarioId == usuarioExistente.Id)
                    .Select(p => new { p.Ativo })
                    .FirstAsync(cancellationToken);

                if (pacienteExistente.Ativo)
                {
                    throw new ConflitoException("paciente.cpf_duplicado", "Já existe paciente ativo com este CPF.");
                }
                throw new ConflitoException("paciente.cpf_desativado", "Existe paciente desativado com este CPF. Reative o cadastro.");
            }

            throw new ConflitoException(
                "paciente.cpf_ja_cadastrado",
                $"CPF já cadastrado como usuário '{usuarioExistente.NomeCompleto}'. Use /pacientes/promover.");
        }

        var agora = DateTime.UtcNow;
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
            TipoPapel = TipoPapel.Paciente,
            Ativo = true,
            CriadoEm = agora,
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
            Ativo = true,
            CriadoEm = agora,
        };

        _db.Usuarios.Add(usuario);
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);

        return paciente.Id;
    }

    public async Task<Guid> PromoverAsync(PromoverPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == request.UsuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), request.UsuarioId);

        if (usuario.TipoPapel is not null)
        {
            throw new ConflitoException(
                "paciente.usuario_ja_tem_papel",
                $"Usuário já tem papel '{usuario.TipoPapel}'. Elimine o papel atual antes de promover.");
        }

        if (string.IsNullOrWhiteSpace(usuario.Cpf))
        {
            throw new ConflitoException(
                "paciente.usuario_sem_cpf",
                "Usuário precisa ter CPF cadastrado para virar paciente.");
        }

        var agora = DateTime.UtcNow;
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
            Ativo = true,
            CriadoEm = agora,
        };

        usuario.TipoPapel = TipoPapel.Paciente;
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

        // Atualizar Usuario (dados pessoais base)
        paciente.Usuario.NomeCompleto = request.NomeCompleto.Trim();
        paciente.Usuario.Rg = NormalizaOpcional(request.Rg, false);
        paciente.Usuario.Sexo = request.Sexo;
        paciente.Usuario.Endereco = request.Endereco?.ParaEntidade();
        paciente.Usuario.Telefone = NormalizaOpcional(request.TelefonePrincipal, false);
        paciente.Usuario.FotoBase64 = NormalizaOpcional(request.FotoBase64, false);
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            paciente.Usuario.Email = request.Email.Trim().ToLowerInvariant();
        }

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
        paciente.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        if (!paciente.Ativo)
        {
            throw new ConflitoException("paciente.ja_inativo", "Paciente já está inativo.");
        }

        paciente.Ativo = false;
        paciente.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        if (paciente.Ativo)
        {
            throw new ConflitoException("paciente.ja_ativo", "Paciente já está ativo.");
        }

        paciente.Ativo = true;
        paciente.AtualizadoEm = DateTime.UtcNow;

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
