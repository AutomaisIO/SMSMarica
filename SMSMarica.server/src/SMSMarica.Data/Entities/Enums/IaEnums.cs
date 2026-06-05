namespace SMSMarica.Data.Entities.Enums;

/// <summary>Tipo de sistema-fonte de uma base cadastrada no módulo IA.</summary>
public enum TipoFonte
{
    Salux = 1,
    Mv = 2,
    Eco = 3,
    Fhir = 4,
    Postgres = 5,
}

/// <summary>Dialeto SQL gerado/executado para a fonte.</summary>
public enum DialetoSql
{
    Oracle = 1,
    Postgres = 2,
}

/// <summary>Ambiente da instância de uma base (rótulo operacional).</summary>
public enum AmbienteFonte
{
    Producao = 1,
    Treinamento = 2,
}

/// <summary>Estado de uma pergunta processada pelo módulo IA.</summary>
public enum StatusConsulta
{
    Gerando = 1,
    Executando = 2,
    Sucesso = 3,
    Erro = 4,
}

/// <summary>Natureza de um item de aprendizado incremental.</summary>
public enum TipoAprendizado
{
    Hint = 1,
    Exemplo = 2,
    Glossario = 3,
    Correcao = 4,
}

/// <summary>Como um aprendizado nasceu: manualmente ou via auto-correção.</summary>
public enum OrigemAprendizado
{
    Manual = 1,
    Auto = 2,
}
