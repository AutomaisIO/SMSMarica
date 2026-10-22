import { Merge } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import { SimulacaoUnificarPaciente } from '@/features/manual/simulacoes/SimulacaoUnificarPaciente';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da unificação de cadastros. Mora na lista de Pacientes (modo "Unificar paciente", rota
 * antiga /app/pacientes/unificar redireciona para /app/pacientes?unificar=1). Conferido no código:
 * `features/pacientes` (PacientesPage, components/UnificarPacientesModal, api usePreverUnificacao/
 * useUnificarPacientes) e no backend `PacientesService.UnificarAsync`/`PreverUnificacaoAsync`,
 * `PatientMergeFhir.UnirContatosDaUnificacao` (contato só acumula), `RepontadorPacienteService`
 * (reaponta smsmarica.*, resolve os índices únicos) e o `$merge` do hub FHIR (identificadores e
 * clínico; o outro cadastro fica inativo apontando para o unificado). Permissão: Pacientes → Edição.
 */
export const artigoPacientesUnificar: Artigo = {
  slug: 'pacientes-unificar',
  titulo: 'Unificar paciente (cadastros duplicados)',
  resumo:
    'Juntar dois cadastros da mesma pessoa num só: marcar os dois na lista, clicar nos dados que devem ficar (telefones e alergias um a um) e aplicar — laudos, agendamentos, solicitações, conversas e histórico clínico vêm todos junto.',
  grupo: 'cadastros',
  icone: Merge,
  rota: '/app/pacientes/unificar',
  publico: 'Quem cuida do cadastro e precisa resolver um paciente que aparece repetido',
  atualizadoEm: '2026-10-22',
  palavrasChave: [
    'unificar',
    'unificação',
    'mesclar',
    'merge',
    'duplicado',
    'duplicata',
    'paciente repetido',
    'cadastro repetido',
    'juntar cadastros',
    'marcar',
    'checkbox',
    'verde',
    'esmaecido',
    'aplicar',
    'telefone verificado',
    'desligar telefone',
    'alergias',
    'agendamentos',
    'CPF diferente',
    'CNS diferente',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'duplicado repetido importação sisreg sem cpf juntar',
      conteudo: (
        <>
          <P>
            Às vezes a mesma pessoa tem <strong>dois cadastros</strong> — quase sempre porque entrou
            por uma importação sem CPF e foi recadastrada depois. Isso parte o prontuário: metade dos
            laudos e agendamentos fica num cadastro, metade no outro. A unificação junta os dois num só.
          </P>
          <P>
            Ela acontece na própria lista de <strong>Pacientes</strong>, pelo botão{' '}
            <BotaoRef variante="outline">Unificar paciente</BotaoRef>. Nada é alterado até você clicar
            em <BotaoRef>Aplicar</BotaoRef>.
          </P>
        </>
      ),
    },
    {
      id: 'marcar-os-dois',
      titulo: 'Marcar os dois cadastros',
      busca: 'checkbox marcar selecionar primeiro segundo esmaecido buscar de novo cancelar unificação',
      conteudo: (
        <>
          <P>
            Com o botão ligado, cada linha da lista ganha uma <strong>caixa de marcar</strong> (clicar
            na linha também marca).
          </P>
          <Lista>
            <Item>
              O <strong>primeiro</strong> cadastro marcado fica <strong>esmaecido</strong> e aparece na
              faixa verde acima da lista. Ele continua guardado mesmo que você faça outra busca — então,
              se o outro cadastro não aparece na mesma pesquisa (nome escrito diferente, por exemplo),
              é só buscar de novo.
            </Item>
            <Item>
              Ao marcar o <strong>segundo</strong>, abre o modal com os dois lado a lado.
            </Item>
            <Item>
              Marcou o errado? Clique nele de novo, ou em <em>Desmarcar</em> na faixa. Para sair,{' '}
              <BotaoRef variante="outline">Cancelar unificação</BotaoRef>.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'escolher-os-dados',
      titulo: 'Escolher os dados que ficam',
      busca: 'verde esmaecido clicar dado valor nome endereço alergias medicamentos telefones um por um desligar importar manter verificado histórico cpf nascimento cns',
      conteudo: (
        <>
          <P>
            No modal, cada dado aparece duas vezes, uma por cadastro, e só existem dois estados:
          </P>
          <Lista>
            <Item>
              <strong>verde, com borda e sombra verde</strong> — este valor vai para o cadastro
              unificado;
            </Item>
            <Item>
              <strong>esmaecido</strong> — este valor não fica.
            </Item>
          </Lista>
          <P>
            Para trocar, clique no valor esmaecido. A tela já vem resolvida do jeito mais seguro, e
            você só clica onde discordar:
          </P>
          <Lista>
            <Item>
              Dado que <strong>só um cadastro tem</strong> já vem verde (o vazio não se escolhe) — a
              unificação completa a ficha com o que faltava.
            </Item>
            <Item>
              Dado <strong>igual nos dois</strong> aparece uma vez só, verde, ocupando as duas colunas.
            </Item>
            <Item>
              <strong>Telefones</strong> e itens de <strong>alergias, medicamentos, comorbidades e
              deficiências</strong> aparecem <strong>um por um</strong>, cada um com o seu verde: clique
              para desligar (não importar / não manter) ou religar. Tudo começa ligado, e o que está
              nos dois cadastros aparece uma vez só.
            </Item>
            <Item>
              O telefone <strong>verificado</strong> (selo “verificado”) fica sempre: não se desliga e
              não é sobrescrito pelo do outro cadastro. Se só o outro cadastro tinha telefone
              verificado, ele passa a ser o verificado do unificado.
            </Item>
            <Item>
              Sempre sobra pelo menos um telefone — um número a menos é uma pessoa que a Secretaria
              deixa de conseguir avisar. O telefone desligado que era do próprio cadastro{' '}
              <strong>não é apagado</strong>: vira histórico. Número marcado como errado continua
              marcado.
            </Item>
            <Item>
              <strong>E-mail</strong> não se escolhe: os dos dois cadastros ficam.
            </Item>
          </Lista>
          <Callout tipo="regra" titulo="O CPF manda">
            O cadastro unificado fica com o <strong>CPF</strong> escolhido — quando só um dos dois tem
            CPF, é esse. CNS e data de nascimento seguem a sua escolha; o CNS que não ficar como
            principal continua guardado, para a pessoa seguir sendo achada por ele.
          </Callout>
          <SimulacaoUnificarPaciente />
        </>
      ),
    },
    {
      id: 'chave-divergente',
      titulo: 'Quando o CPF ou o CNS são diferentes',
      busca: 'cpf diferente cns diferente pessoas diferentes confirmar',
      conteudo: (
        <>
          <P>
            Se os dois cadastros têm <strong>CPF (ou CNS) diferentes</strong>, o modal mostra um aviso
            vermelho. Documentos diferentes podem significar que são <strong>pessoas diferentes</strong>
            {' '}— e unir pessoas diferentes mistura dois prontuários, o que não se desfaz num clique.
          </P>
          <P>
            Nesse caso o <BotaoRef>Aplicar</BotaoRef> só libera depois de marcar{' '}
            <em>“Confirmo que é a mesma pessoa”</em>. Na dúvida, não una: confira antes com o documento
            em mãos.
          </P>
        </>
      ),
    },
    {
      id: 'o-que-e-movido',
      titulo: 'O que vem junto e o registro',
      busca: 'laudos solicitações agendamentos guias regulação conversas acervo histórico clínico app do cidadão auditoria histórico de alterações aplicar de novo sai da busca lista telefone whatsapp link antigo abrir o cadastro unificado',
      conteudo: (
        <>
          <P>
            No pé do modal, a linha <strong>“Vem junto”</strong> conta o que está no outro cadastro e
            vai passar para o unificado: solicitações e agendamentos do SISREG, agendamentos do SER,
            SERNIT e ESUS, guias, regulação, laudos, conversas de WhatsApp, acervo de exames, ouvidoria,
            app do cidadão, transporte e o restante. O histórico clínico (atendimentos, diagnósticos,
            medicações, documentos) vem junto sempre.
          </P>
          <P>
            Depois do <BotaoRef>Aplicar</BotaoRef>, o resultado aparece <strong>no próprio modal</strong>
            {' '}— quantos registros vieram, quantos telefones entraram e quantos foram desligados — e fica ali até você
            fechar. De lá, <BotaoRef>Ver cadastro</BotaoRef> abre a ficha unificada.
          </P>
          <Lista>
            <Item>
              O outro cadastro <strong>não é apagado</strong>: fica desativado e apontando para o
              unificado. Ele <strong>sai das buscas</strong> (por nome, CPF e telefone — inclusive a do
              WhatsApp, que passa a achar só o unificado) e não pode ser unificado de novo. Quem o abrir
              por um link antigo vê um aviso com o botão{' '}
              <BotaoRef variante="ghost">Abrir o cadastro unificado</BotaoRef>.
            </Item>
            <Item>
              Se os dois cadastros tinham acesso ao <strong>app do cidadão</strong>, vale o do
              unificado; o do outro é desligado.
            </Item>
            <Item>
              A ação fica registrada no <strong>Histórico de alterações</strong> dos dois cadastros
              (quem fez, quando e o quanto veio junto).
            </Item>
            <Item>
              Deu erro no meio? Marque os dois e aplique de novo: o que já tinha passado não se repete.
            </Item>
          </Lista>
          <Callout tipo="atencao">
            Só quem tem permissão de <strong>edição</strong> em Pacientes consegue unificar.
          </Callout>
        </>
      ),
    },
  ],
};
