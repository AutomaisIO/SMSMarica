import type { ReactNode } from 'react';
import { ArrowLeft, MapPin, Pencil, Phone } from 'lucide-react';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { MapaSeletor } from '@/shared/ui/MapaSeletor';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { useUnidadePorId, useUsuariosDaUnidade } from '@/features/unidades/api/queries';
import { UsuariosDaUnidadeSecao } from '@/features/unidades/components/UsuariosDaUnidadeSecao';
import { EquipamentosDaUnidadeSecao } from '@/features/unidades/components/EquipamentosDaUnidadeSecao';
import { PesquisaSatisfacaoAba } from '@/features/pesquisa-satisfacao/components/PesquisaSatisfacaoAba';
import { MapeamentoSisregSecao } from '@/features/sisreg-mapeamento/components/MapeamentoSisregSecao';
import { SincronismoSisregSecao } from '@/features/sisreg-mapeamento/components/SincronismoSisregSecao';
import { useListarEquipamentos } from '@/features/equipamentos/api/queries';
import { useListarTratamentos } from '@/features/tratamentos/api/queries';
import type { TratamentoListItem } from '@/features/tratamentos/types';
import { formatarDataBr } from '@/features/tratamentos/lib/expansor';

function Dado({ rotulo, valor }: { rotulo: string; valor?: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className="text-sm text-gray-900">
        {valor !== null && valor !== undefined && valor !== '' ? (
          valor
        ) : (
          <span className="text-gray-400">—</span>
        )}
      </span>
    </div>
  );
}

function montarEnderecoLinha(e: {
  logradouro: string;
  numero: string | null;
  complemento: string | null;
  bairro: string;
  cidade: string;
  uf: string;
  cep: string;
}): string {
  const partes: string[] = [];
  if (e.logradouro) partes.push(e.logradouro + (e.numero ? `, ${e.numero}` : ''));
  if (e.complemento) partes.push(e.complemento);
  if (e.bairro) partes.push(e.bairro);
  if (e.cidade || e.uf) partes.push(`${e.cidade}${e.uf ? `/${e.uf}` : ''}`);
  if (e.cep) partes.push(`CEP ${e.cep}`);
  return partes.join(' · ');
}

export function UnidadeDetalhePage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const id = params.id ?? '';

  const detalhe = useUnidadePorId(id || null);
  const tratamentos = useListarTratamentos({ unidadeId: id });
  const usuariosDaUnidade = useUsuariosDaUnidade(id || null);
  const equipamentosDaUnidade = useListarEquipamentos(id || undefined, false);
  const podeGerirSisreg = useTemConsulta('SisregMapeamento');
  // Consulta abre a aba; ligar o motor, mexer nos checkboxes e disparar varredura exigem Edição —
  // uma varredura fora de hora derruba a sessão de quem está atendendo pela unidade.
  const podeEditarSisreg = usePermissao('SisregMapeamento', 'Edicao');

  const u = detalhe.data;

  const colunas: Coluna<TratamentoListItem>[] = [
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (t) => (
        <button
          type="button"
          onClick={() => navigate(`/app/tratamentos/${t.id}`)}
          className="text-left font-medium text-red-700 hover:underline"
        >
          {t.pacienteNome}
        </button>
      ),
    },
    {
      chave: 'tipo',
      cabecalho: 'Tipo',
      render: (t) => t.tipoTratamentoNome ?? t.descricao,
    },
    {
      chave: 'proxima',
      cabecalho: 'Próxima sessão',
      render: (t) => (t.proximaSessao ? formatarDataBr(t.proximaSessao) : '—'),
    },
    {
      chave: 'progresso',
      cabecalho: 'Progresso',
      render: (t) => (
        <span className="text-xs text-gray-600">
          {t.sessoesRealizadas}/{t.totalSessoes}
        </span>
      ),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (t) => <StatusBadge ativo={t.ativo} />,
    },
  ];

  const abaTratamentos = (
    <div className="space-y-3">
      {tratamentos.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(tratamentos.error)}
        </div>
      ) : null}
      <Tabela
        colunas={colunas}
        dados={tratamentos.data ?? []}
        chaveLinha={(t) => t.id}
        carregando={tratamentos.isLoading}
        vazio={
          !tratamentos.isLoading && (tratamentos.data?.length ?? 0) === 0
            ? 'Nenhum tratamento cadastrado nesta unidade.'
            : undefined
        }
      />
    </div>
  );

  const podeVerPesquisa = useTemConsulta('PesquisaSatisfacao');

  const abas: Aba[] = [
    {
      id: 'tratamentos',
      rotulo: 'Tratamentos',
      conteudo: abaTratamentos,
      badge: tratamentos.data?.length || undefined,
    },
    {
      id: 'equipamentos',
      rotulo: 'Equipamentos',
      conteudo: <EquipamentosDaUnidadeSecao unidadeId={id} />,
      badge: equipamentosDaUnidade.data?.length || undefined,
    },
    {
      id: 'usuarios',
      rotulo: 'Usuários',
      conteudo: <UsuariosDaUnidadeSecao unidadeId={id} />,
      badge: usuariosDaUnidade.data?.length || undefined,
    },
    ...(podeGerirSisreg && id
      ? [
          {
            id: 'sisreg',
            rotulo: 'SISREG',
            conteudo: (
              <div className="space-y-4">
                <p className="text-sm text-gray-600">
                  Profissionais e procedimentos desta unidade no SISREG, e o sincronismo diário da
                  agenda dela. A credencial é uma só, global, e fica em Sistema → Integrações.
                </p>
                <MapeamentoSisregSecao unidadeId={id} podeEditar={podeEditarSisreg} />
                <SincronismoSisregSecao unidadeId={id} podeEditar={podeEditarSisreg} />
              </div>
            ),
          } satisfies Aba,
        ]
      : []),
    ...(podeVerPesquisa && id
      ? [
          {
            id: 'pesquisa',
            rotulo: 'Pesquisa de satisfação',
            conteudo: <PesquisaSatisfacaoAba unidadeId={id} />,
          } satisfies Aba,
        ]
      : []),
  ];

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/app/unidades')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">
                {u?.nome ?? 'Carregando…'}
              </h1>
              {u ? <StatusBadge ativo={u.ativo} /> : null}
            </div>
            {u?.endereco ? (
              <p className="mt-1 flex items-center gap-1 text-sm text-gray-500">
                <MapPin className="h-3.5 w-3.5" />
                {montarEnderecoLinha(u.endereco)}
              </p>
            ) : null}
          </div>
        </div>
        {u ? (
          <Button variante="outline" onClick={() => navigate(`/app/unidades/${id}/editar`)}>
            <Pencil className="h-4 w-4" /> Editar
          </Button>
        ) : null}
      </header>

      {detalhe.isLoading ? (
        <div className="text-sm text-gray-500">Carregando unidade…</div>
      ) : detalhe.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(detalhe.error)}
        </div>
      ) : u ? (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
            <h2 className="mb-4 text-sm font-semibold text-gray-900">Identificação</h2>
            <div className="grid grid-cols-1 gap-4">
              <Dado rotulo="Nome" valor={u.nome} />
              <Dado rotulo="CNES" valor={u.cnes ?? undefined} />
              <Dado rotulo="Telefone" valor={u.telefone ? <TelefoneCopiavel numero={u.telefone} /> : undefined} />
              <Dado rotulo="Cadastrada em" valor={new Date(u.criadoEm).toLocaleString('pt-BR')} />
            </div>
          </section>

          <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
            <h2 className="mb-4 text-sm font-semibold text-gray-900">Endereço e localização</h2>
            {u.endereco ? (
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <Dado rotulo="CEP" valor={u.endereco.cep} />
                <Dado rotulo="UF" valor={u.endereco.uf} />
                <Dado rotulo="Logradouro" valor={u.endereco.logradouro} />
                <Dado rotulo="Número" valor={u.endereco.numero ?? undefined} />
                <Dado rotulo="Complemento" valor={u.endereco.complemento ?? undefined} />
                <Dado rotulo="Bairro" valor={u.endereco.bairro} />
                <Dado rotulo="Cidade" valor={u.endereco.cidade} />
                <Dado rotulo="Ponto de referência" valor={u.endereco.pontoReferencia ?? undefined} />
              </div>
            ) : (
              <p className="text-sm text-gray-400">Endereço não informado.</p>
            )}
            {u.latitude != null && u.longitude != null ? (
              <div className="mt-4">
                <p className="mb-2 flex items-center gap-1 text-xs text-gray-500">
                  <Phone className="hidden h-3.5 w-3.5" />
                  GPS: {u.latitude.toFixed(6)}, {u.longitude.toFixed(6)}
                </p>
                <MapaSeletor
                  valor={{ lat: u.latitude, lng: u.longitude }}
                  aoMudar={() => undefined}
                  altura={240}
                  desabilitado
                />
              </div>
            ) : null}
          </section>
        </div>
      ) : null}

      <section>
        <Tabs abas={abas} inicial="tratamentos" />
      </section>
    </div>
  );
}
