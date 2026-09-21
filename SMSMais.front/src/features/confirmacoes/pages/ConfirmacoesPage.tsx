import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { CalendarCheck2, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao, useTemConsulta } from '@/shared/auth/authStore';
import { Input } from '@/shared/ui/Input';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Tabs } from '@/shared/ui/Tabs';
import {
  useAcaoAtendimento,
  useAtendimento,
  useMotivosTelefoneComprometido,
  useResumoAbas,
} from '@/features/confirmacoes/api';
import { ModalLoginSisreg } from '@/features/confirmacoes/components/ModalLoginSisreg';
import { notificar } from '@/shared/ui/Notificacoes';
import { useSessaoSisregObrigatoria } from '@/features/confirmacoes/lib/sessaoSisreg';
import { AbaEquipe } from '@/features/confirmacoes/components/AbaEquipe';
import { CardSolicitacao, type AcaoCard } from '@/features/confirmacoes/components/CardSolicitacao';
import {
  ModalCancelar,
  ModalConfirmar,
  ModalContatoCorrigido,
  ModalContatoErrado,
  ModalPendente,
  ModalTransferir,
} from '@/features/confirmacoes/components/ModaisAtendimento';
import type { AcaoResultado, AbaAtendimento, SolicitacaoAtendimento } from '@/features/confirmacoes/types';
import { useRegrasUnidades } from '@/features/mensageria/api/queries';
import { ROTULO_STATUS, useDebounce } from '@/features/mensageria/lib/rotulos';
import type { StatusNotificacao } from '@/features/mensageria/types';

const ABAS: { id: AbaAtendimento; rotulo: string; descricao: string }[] = [
  { id: 'NaoConfirmados', rotulo: 'Não confirmados', descricao: 'Tudo que ainda não foi confirmado por nenhum canal, do agendamento mais próximo para o mais distante.' },
  { id: 'Confirmados', rotulo: 'Confirmados', descricao: 'Confirmados por link, botão, robô, app, recepção ou atendente. Ainda dá para cancelar.' },
  { id: 'ContatoErrado', rotulo: 'Contato errado', descricao: 'Quem atende disse que não é o paciente. Corrija o telefone e verifique para voltar à fila.' },
  { id: 'Pendentes', rotulo: 'Pendentes', descricao: 'Estacionadas por uma atendente com motivo (não atendeu, ligar depois…).' },
  {
    id: 'Cancelamento',
    rotulo: 'Cancelamento',
    descricao:
      'Pediram para cancelar e ninguém tratou ainda — veio do botão do WhatsApp, do robô, do app ou da recepção, tanto faz. ' +
      'Leia o contexto da conversa antes: "não quero cancelar" e "qual o motivo do cancelamento?" chegam parecidos com "quero cancelar". ' +
      'O cancelamento só acontece quando você clicar.',
  },
  {
    id: 'TelefoneComprometido',
    rotulo: 'Telefone comprometido',
    descricao:
      'O sistema não consegue avisar por WhatsApp: ou o cadastro não tem celular, ou o número não está no WhatsApp. ' +
      'Não é contato errado — o número pode ser do paciente. Aqui o caminho é ligar e corrigir o cadastro.',
  },
];

/** Fora de `ABAS` de propósito: não é fila de atendimento e só existe para quem tem o módulo. */
const ABA_EQUIPE = 'Equipe';

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
  // A aba Equipe é de gestão: sem o módulo próprio ela nem aparece (e o endpoint responde 403).
  const veEquipe = useTemConsulta('ConfirmacoesEquipe');

  const [params, setParams] = useSearchParams();
  const abaParam = params.get('aba');
  const aba: AbaAtendimento = ABAS.some((a) => a.id === abaParam) ? (abaParam as AbaAtendimento) : 'NaoConfirmados';
  const naEquipe = veEquipe && abaParam === ABA_EQUIPE;

  const [texto, setTexto] = useState('');
  const [unidadeId, setUnidadeId] = useState('');
  const [envio, setEnvio] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(50);
  const [modal, setModal] = useState<ModalAberto>(null);
  // O desfecho do cancelamento fica NO modal, não num toast. Ver `ResultadoDoCancelamento`.
  const [resultadoCancelamento, setResultadoCancelamento] = useState<AcaoResultado | null>(null);
  // O cancelamento acontece no SISREG PRIMEIRO, com o login da própria atendente, e só vale aqui
  // se lá confirmar. Não há mais estado "cancelado aqui e não lá" — por isso não há mais aviso.
  const sessaoSisreg = useSessaoSisregObrigatoria();
  const textoDeb = useDebounce(texto);

  useEffect(() => setPagina(1), [aba, textoDeb, unidadeId, envio, tamanho]);

  const resumo = useResumoAbas();
  const motivos = useMotivosTelefoneComprometido(aba === 'TelefoneComprometido');
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
    // Desfazer um pedido não destrói nada — devolve a ficha para a fila. Pedir confirmação num
    // ato reversível só ensina a atendente a clicar em "sim" sem ler.
    if (tipo === 'desfazer-pedido') {
      acao.mutate(
        { solicitacaoId: item.solicitacaoId, acao: { tipo: 'desfazer-pedido-cancelamento' } },
        // Esta ação não abre modal: sem o aviso, o card some da aba e nada explica por quê.
        { onSuccess: () => notificar('Pedido desconsiderado — a ficha voltou para a fila.', 'sucesso') },
      );
      return;
    }
    acao.reset();
    setResultadoCancelamento(null);
    setModal({ tipo, item });
  }

  function fecharModal() {
    setModal(null);
    setResultadoCancelamento(null);
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

      {aba === 'TelefoneComprometido' && motivos.data ? (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          <Porque
            titulo="Sem celular no cadastro"
            valor={motivos.data.semCelular}
            detalhe="Não há número para tentar. Pegue na próxima passagem pela unidade."
          />
          <Porque
            titulo="Número não é WhatsApp"
            valor={motivos.data.naoEhWhatsApp}
            detalhe="A Meta recusou a entrega (131026). O número pode atender ligação."
          />
          <Porque titulo="Agendamentos parados" valor={motivos.data.total} detalhe="Vagas em risco por falta de aviso." />
          <Porque
            titulo="Pessoas a contatar"
            valor={motivos.data.pacientesDistintos}
            detalhe="Cada uma é um telefonema, mesmo com vários exames."
          />
        </div>
      ) : null}

      {acao.isError && !modal ? <p className="text-sm text-red-700">{extrairMensagemDeErro(acao.error)}</p> : null}
      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}

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
            <div className="flex items-center gap-1.5">
              <h1 className="text-2xl font-semibold text-gray-900">Confirmações</h1>
              <AjudaManual artigo="confirmacoes" />
            </div>
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
        abaAtiva={naEquipe ? ABA_EQUIPE : aba}
        aoTrocarAba={trocarAba}
        abas={[...ABAS.map((a) => ({
          id: a.id,
          rotulo: a.rotulo,
          badge: r
            ? a.id === 'NaoConfirmados' ? r.naoConfirmados
              : a.id === 'Confirmados' ? r.confirmados
                : a.id === 'ContatoErrado' ? r.contatoErrado
                  : a.id === 'Pendentes' ? r.pendentes
                    : a.id === 'Cancelamento' ? r.cancelamento
                      : r.telefoneComprometido
            : undefined,
          conteudo,
        })),
        ...(veEquipe ? [{ id: ABA_EQUIPE, rotulo: 'Equipe', conteudo: <AbaEquipe /> }] : []),
        ]}
      />

      {modal?.tipo === 'confirmar' ? (
        <ModalConfirmar
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoConfirmar={(meio, observacao) =>
            acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'confirmar', meio, observacao } }, { onSuccess: () => { fecharModal(); notificar('Presença confirmada.', 'sucesso'); } })}
        />
      ) : null}
      {modal?.tipo === 'cancelar' ? (
        <ModalCancelar
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          resultado={resultadoCancelamento}
          aoCancelar={(motivo, meio) => {
            // O cancelamento vai ao SISREG assinado pela atendente. Sem sessão lá, o modal de
            // senha aparece e a ação é retomada sozinha — ela não redigita o motivo.
            const cancelar = () =>
              acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'cancelar', motivo, meio } }, {
                // NÃO fecha: o modal vira o desfecho. Fechar aqui e dizer o resultado num toast
                // fazia a tela apagar tudo no exato instante em que tinha algo a contar — e o
                // toast morre em quatro segundos, no canto oposto ao que a pessoa olhava.
                onSuccess: setResultadoCancelamento,
                onError: (erro) => sessaoSisreg.tratouFaltaDeSessao(erro, cancelar),
              });
            sessaoSisreg.comSessao(cancelar);
          }}
        />
      ) : null}
      <ModalLoginSisreg {...sessaoSisreg.modal} />
      {modal?.tipo === 'pendente' ? (
        <ModalPendente
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoEnviar={(motivo) => acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'pendente', motivo } }, { onSuccess: () => { fecharModal(); notificar('Ficha estacionada como pendente.', 'sucesso'); } })}
        />
      ) : null}
      {modal?.tipo === 'contato-errado' ? (
        <ModalContatoErrado
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoRegistrar={(observacao) => acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'contato-errado', observacao } }, { onSuccess: () => { fecharModal(); notificar('Contato marcado como errado.', 'sucesso'); } })}
        />
      ) : null}
      {modal?.tipo === 'transferir' ? (
        <ModalTransferir
          item={modal.item} ocupado={acao.isPending} erro={acao.error} aoFechar={fecharModal}
          aoTransferir={(paraUsuarioId, observacao) =>
            acao.mutate({ solicitacaoId: modal.item.solicitacaoId, acao: { tipo: 'transferir', paraUsuarioId, observacao } }, { onSuccess: () => { fecharModal(); notificar('Atendimento transferido.', 'sucesso'); } })}
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

/**
 * Um número do painel de porquês. Cada card responde "quantos" e, logo abaixo, "e daí" — sem a
 * segunda linha o número não diz à atendente o que fazer com ele.
 */
function Porque({ titulo, valor, detalhe }: { titulo: string; valor: number; detalhe: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-3 py-2">
      <p className="text-xs text-gray-500">{titulo}</p>
      <p className="text-xl font-semibold text-gray-900">{valor.toLocaleString('pt-BR')}</p>
      <p className="mt-0.5 text-xs leading-snug text-gray-500">{detalhe}</p>
    </div>
  );
}
