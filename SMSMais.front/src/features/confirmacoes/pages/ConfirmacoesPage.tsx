import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { CalendarCheck2, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { Input } from '@/shared/ui/Input';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { Tabs } from '@/shared/ui/Tabs';
import { useAcaoAtendimento, useAtendimento, useResumoAbas } from '@/features/confirmacoes/api';
import { CardSolicitacao, type AcaoCard } from '@/features/confirmacoes/components/CardSolicitacao';
import {
  ModalCancelar,
  ModalConfirmar,
  ModalContatoCorrigido,
  ModalContatoErrado,
  ModalPendente,
  ModalTransferir,
} from '@/features/confirmacoes/components/ModaisAtendimento';
import type { AbaAtendimento, SolicitacaoAtendimento } from '@/features/confirmacoes/types';
import { useRegrasUnidades } from '@/features/mensageria/api/queries';
import { ROTULO_STATUS, useDebounce } from '@/features/mensageria/lib/rotulos';
import type { StatusNotificacao } from '@/features/mensageria/types';

const ABAS: { id: AbaAtendimento; rotulo: string; descricao: string }[] = [
  { id: 'NaoConfirmados', rotulo: 'Não confirmados', descricao: 'Tudo que ainda não foi confirmado por nenhum canal, do agendamento mais próximo para o mais distante.' },
  { id: 'Confirmados', rotulo: 'Confirmados', descricao: 'Confirmados por link, botão, robô, app, recepção ou atendente. Ainda dá para cancelar.' },
  { id: 'ContatoErrado', rotulo: 'Contato errado', descricao: 'Quem atende disse que não é o paciente. Corrija o telefone e verifique para voltar à fila.' },
  { id: 'Pendentes', rotulo: 'Pendentes', descricao: 'Estacionadas por uma atendente com motivo (não atendeu, ligar depois…).' },
];

const TAMANHOS = [25, 50, 100] as const;

type ModalAberto = { tipo: Exclude<AcaoCard, 'atender' | 'assumir' | 'liberar'>; item: SolicitacaoAtendimento } | null;

/**
 * Confirmações — o trabalho das atendentes, no lugar da planilha: quatro filas derivadas do que o
 * sistema já sabe (envio automático, resposta do paciente, número negado) mais a posse humana
 * ("estou atendendo esta"). A gestão dos envios fica na Mensageria.
 */
export function ConfirmacoesPage() {
  const podeEditar = usePermissao('Confirmacoes', 'Edicao');
  const podeCancelar = usePermissao('Confirmacoes', 'Exclusao');
  const veMensageria = useTemConsulta('NotificacoesAgendamento');

  const [params, setParams] = useSearchParams();
  const abaParam = params.get('aba');
  const aba: AbaAtendimento = ABAS.some((a) => a.id === abaParam) ? (abaParam as AbaAtendimento) : 'NaoConfirmados';

  const [texto, setTexto] = useState('');
  const [unidadeId, setUnidadeId] = useState('');
  const [envio, setEnvio] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(50);
  const [modal, setModal] = useState<ModalAberto>(null);
  const [avisoSisreg, setAvisoSisreg] = useState<SolicitacaoAtendimento | null>(null);
  const textoDeb = useDebounce(texto);

  useEffect(() => setPagina(1), [aba, textoDeb, unidadeId, envio, tamanho]);

  const resumo = useResumoAbas();
  const unidades = useRegrasUnidades();
  const q = useAtendimento({
    aba,
    texto: textoDeb.trim() || undefined,
    unidadeId: unidadeId || undefined,
    envio: envio || undefined,
    pagina,
    tamanho,
  });
  const acao = useAcaoAtendimento();

  const descricao = useMemo(() => ABAS.find((a) => a.id === aba)?.descricao, [aba]);

  function executar(tipo: AcaoCard, item: SolicitacaoAtendimento) {
    if (tipo === 'atender' || tipo === 'assumir' || tipo === 'liberar') {
      acao.mutate({ solicitacaoId: item.solicitacaoId, acao: { tipo } });
      return;
    }
    acao.reset();
    setModal({ tipo, item });
  }

  function fecharModal() {
    setModal(null);
    acao.reset();
  }

  function trocarAba(id: string) {
    setParams((p) => { p.set('aba', id); return p; }, { replace: true });
  }

  const conteudo = (
    <div className="space-y-4">
      <p className="text-sm text-gray-600">{descricao}</p>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-5">
        <div className="relative md:col-span-2">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Nome, CPF, CNS ou nº SISREG…" className="pl-9" />
        </div>
        <Select value={unidadeId} onChange={(e) => setUnidadeId(e.target.value)} aria-label="Unidade" className="md:col-span-2">
          <option value="">Todas as unidades</option>
          {(unidades.data ?? []).map((u) => (
            <option key={u.unidadeId} value={u.unidadeId}>{u.unidadeNome}</option>
          ))}
        </Select>
        <Select value={envio} onChange={(e) => setEnvio(e.target.value)} aria-label="Envio automático">
          <option value="">Envio automático: todos</option>
          <option value="NaoEnviada">Não enviada</option>
          {(Object.keys(ROTULO_STATUS) as StatusNotificacao[]).map((s) => (
            <option key={s} value={s}>{ROTULO_STATUS[s]}</option>
          ))}
        </Select>
      </div>

      {acao.isError && !modal ? <p className="text-sm text-red-700">{extrairMensagemDeErro(acao.error)}</p> : null}
      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}

      {avisoSisreg ? (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <span>
            Cancelado no SMSMais: <strong>{avisoSisreg.pacienteNome}</strong>
            {avisoSisreg.codigoSolicitacao ? ` (SISREG ${avisoSisreg.codigoSolicitacao})` : ''}. Agora cancele também no
            SISREG pelo navegador — com a extensão instalada, o sistema concilia sozinho.
          </span>
          <button type="button" className="text-xs underline" onClick={() => setAvisoSisreg(null)}>entendi</button>
        </div>
      ) : null}

      {q.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : (q.data?.itens.length ?? 0) === 0 ? (
        <p className="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-500">Nada nesta fila.</p>
      ) : (
        <div className="space-y-2">
          {q.data!.itens.map((item) => (
            <CardSolicitacao
              key={item.solicitacaoId}
              item={item}
              aba={aba}
              podeEditar={podeEditar}
              podeCancelar={podeCancelar}
              ocupado={acao.isPending}
              aoAcao={executar}
            />
          ))}
        </div>
      )}

      {q.data ? (
        <Paginacao pagina={pagina} tamanho={tamanho} total={q.data.total} tamanhos={TAMANHOS} aoMudarPagina={setPagina} aoMudarTamanho={setTamanho} />
      ) : null}
    </div>
  );

  const r = resumo.data;

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <CalendarCheck2 className="mt-1 h-6 w-6 text-red-600" />
          <div>
            <h1 className="text-2xl font-semibold text-gray-900">Confirmações</h1>
            <p className="mt-1 text-sm text-gray-600">
              Agendamentos do SISREG que precisam de confirmação. Quem atende puxa o card, fala com o paciente e dá o
              desfecho: confirmado, cancelado, pendente ou contato errado.
              {r && r.emAtendimentoComigo > 0 ? <> Você está com <strong>{r.emAtendimentoComigo}</strong> em atendimento.</> : null}
            </p>
          </div>
        </div>
        {veMensageria ? (
          <Link to="/app/mensageria" className="shrink-0 text-sm text-red-700 hover:underline">Mensageria (envios, lote, regras) →</Link>
        ) : null}
      </header>

      <Tabs
        abaAtiva={aba}
        aoTrocarAba={trocarAba}
        abas={ABAS.map((a) => ({
          id: a.id,
          rotulo: a.rotulo,
          badge: r
            ? a.id === 'NaoConfirmados' ? r.naoConfirmados
              : a.id === 'Confirmados' ? r.confirmados
                : a.id === 'ContatoErrado' ? r.contatoErrado
                  : r.pendentes
            : undefined,
          conteudo,
        }))}
      />

      {modal?.tipo === 'confirmar' ? (
        <ModalConfirmar
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoConfirmar={(meio, observacao) =>
            acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'confirmar', meio, observacao } }, { onSuccess: fecharModal })}
        />
      ) : null}
      {modal?.tipo === 'cancelar' ? (
        <ModalCancelar
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoCancelar={(motivo, meio) =>
            acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'cancelar', motivo, meio } }, {
              onSuccess: (res) => { fecharModal(); if (res.orientacaoSisreg) setAvisoSisreg(modal.item); },
            })}
        />
      ) : null}
      {modal?.tipo === 'pendente' ? (
        <ModalPendente
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoEnviar={(motivo) => acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'pendente', motivo } }, { onSuccess: fecharModal })}
        />
      ) : null}
      {modal?.tipo === 'contato-errado' ? (
        <ModalContatoErrado
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoRegistrar={(observacao) => acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'contato-errado', observacao } }, { onSuccess: fecharModal })}
        />
      ) : null}
      {modal?.tipo === 'transferir' ? (
        <ModalTransferir
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoTransferir={(paraUsuarioId, observacao) =>
            acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'transferir', paraUsuarioId, observacao } }, { onSuccess: fecharModal })}
        />
      ) : null}
      {modal?.tipo === 'contato-corrigido' ? (
        <ModalContatoCorrigido
          item={modal.item} aoFechar={fecharModal}
          aoCorrigido={(telefone) => acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'contato-corrigido', telefone } }, { onSettled: fecharModal })}
        />
      ) : null}
    </div>
  );
}
