using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Pacientes;

public sealed class PacientesService(SmsMaricaDbContext db) : IPacientesService
{
    private const int LimiteBusca = 10;
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Paciente> query = _db.Pacientes.AsNoTracking().Where(p => p.Ativo);

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
            query = query.Where(p => EF.Functions.Like(p.Cpf, digitos + "%") || EF.Functions.Like(p.Cpf, "%" + digitos + "%"));
        }
        else
        {
            // Tokenização por espaço — cada token precisa aparecer em alguma parte do nome (ILIKE).
            var tokens = termo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var like = "%" + token + "%";
                query = query.Where(p => EF.Functions.ILike(p.NomeCompleto, like));
            }
        }

        var encontrados = await query
            .OrderBy(p => p.NomeCompleto)
            .Take(LimiteBusca)
            .ToListAsync(cancellationToken);

        return [.. encontrados.Select(PacientesMapper.ParaListItem)];
    }

    public async Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .AsNoTracking()
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
            .Where(p => p.Cpf == normalizado)
            .Select(p => new PacienteExistenciaDto(p.Id, p.NomeCompleto, p.Cpf, p.Ativo))
            .FirstOrDefaultAsync(cancellationToken);

        return paciente;
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = NormalizarDigitos(request.Cpf);

        var existente = await _db.Pacientes
            .AsNoTracking()
            .Where(p => p.Cpf == cpfNormalizado)
            .Select(p => new { p.Id, p.Ativo })
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is not null)
        {
            if (existente.Ativo)
            {
                throw new ConflitoException("paciente.cpf_duplicado", "Já existe paciente ativo com este CPF.");
            }
            // Há paciente desativado com esse CPF — front deve chamar reativar.
            throw new ConflitoException("paciente.cpf_desativado", "Existe paciente desativado com este CPF. Reative o cadastro.");
        }

        var paciente = new Paciente
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Cpf = cpfNormalizado,
            Cns = NormalizaOpcional(request.Cns, true),
            Rg = NormalizaOpcional(request.Rg, false),
            DataNascimento = request.DataNascimento,
            Sexo = request.Sexo,
            EstadoCivil = request.EstadoCivil,
            RacaCor = request.RacaCor,
            Escolaridade = request.Escolaridade,
            Ocupacao = NormalizaOpcional(request.Ocupacao, false),
            Naturalidade = NormalizaOpcional(request.Naturalidade, false),
            Nacionalidade = string.IsNullOrWhiteSpace(request.Nacionalidade) ? "Brasileira" : request.Nacionalidade!.Trim(),
            NomeDaMae = NormalizaOpcional(request.NomeDaMae, false),
            NomeDoPai = NormalizaOpcional(request.NomeDoPai, false),
            ResponsavelLegal = NormalizaOpcional(request.ResponsavelLegal, false),
            Endereco = request.Endereco?.ParaEntidade(),
            TelefonePrincipal = NormalizaOpcional(request.TelefonePrincipal, false),
            TelefoneCelular = NormalizaOpcional(request.TelefoneCelular, false),
            TelefoneResidencial = NormalizaOpcional(request.TelefoneResidencial, false),
            Email = NormalizaOpcional(request.Email, false),
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
            FotoBase64 = NormalizaOpcional(request.FotoBase64, false),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);

        return paciente.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        paciente.NomeCompleto = request.NomeCompleto.Trim();
        paciente.Cns = NormalizaOpcional(request.Cns, true);
        paciente.Rg = NormalizaOpcional(request.Rg, false);
        paciente.Sexo = request.Sexo;
        paciente.EstadoCivil = request.EstadoCivil;
        paciente.RacaCor = request.RacaCor;
        paciente.Escolaridade = request.Escolaridade;
        paciente.Ocupacao = NormalizaOpcional(request.Ocupacao, false);
        paciente.Naturalidade = NormalizaOpcional(request.Naturalidade, false);
        paciente.Nacionalidade = string.IsNullOrWhiteSpace(request.Nacionalidade) ? "Brasileira" : request.Nacionalidade!.Trim();
        paciente.NomeDaMae = NormalizaOpcional(request.NomeDaMae, false);
        paciente.NomeDoPai = NormalizaOpcional(request.NomeDoPai, false);
        paciente.ResponsavelLegal = NormalizaOpcional(request.ResponsavelLegal, false);
        paciente.Endereco = request.Endereco?.ParaEntidade();
        paciente.TelefonePrincipal = NormalizaOpcional(request.TelefonePrincipal, false);
        paciente.TelefoneCelular = NormalizaOpcional(request.TelefoneCelular, false);
        paciente.TelefoneResidencial = NormalizaOpcional(request.TelefoneResidencial, false);
        paciente.Email = NormalizaOpcional(request.Email, false);
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
        paciente.FotoBase64 = NormalizaOpcional(request.FotoBase64, false);
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
}
