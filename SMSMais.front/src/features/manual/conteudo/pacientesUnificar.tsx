import { Merge } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da tela Unificar paciente (/app/pacientes/unificar). Conferido no código:
 * `features/pacientes` (UnificarPacientePage, api/pacientesApi preverUnificacao/unificarPacientes,
 * queries usePreverUnificacao/useUnificarPacientes) e no backend
 * `PacientesService.UnificarAsync`/`PreverUnificacaoAsync`, `RepontadorPacienteService` (reaponta
 * smsmarica.*) e o `$merge` do hub FHIR (move identificadores e o clínico, marca o absorvido
 * inativo). Permissão: Pacientes → Edição.
 */
export const artigoPacientesUnificar: Artigo = {
  slug: 'pacientes-unificar',
  titulo: 'Unificar paciente (cadastros duplicados)',
  resumo:
    'Juntar dois cadastros da mesma pessoa num só: escolher qual fica, decidir dado a dado o que prevalece e mover tudo — laudos, solicitações, conversas e histórico clínico — para o cadastro definitivo.',
  grupo: 'cadastros',
  icone: Merge,
  rota: '/app/pacientes/unificar',
  publico: 'Quem cuida do cadastro e precisa resolver um paciente que aparece repetido',
  atualizadoEm: '2026-10-06',
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
    'absorver',
    'sobrevivente',
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
            laudos e exames fica num cadastro, metade no outro. Esta tela junta os dois num só.
          </P>
          <P>
            Você abre pelo botão <BotaoRef>Unificar paciente</BotaoRef> na lista de Pacientes. Nada é
            alterado até você revisar tudo e confirmar.
          </P>
        </>
      ),
    },
    {
      id: 'os-dois-lados',
      titulo: 'Escolher quem fica e quem é absorvido',
      busca: 'lado sobrevivente absorvido trocar',
      conteudo: (
        <>
          <P>
            Você busca e escolhe dois cadastros:
          </P>
          <Lista>
            <Item>
              <strong>Cadastro que vai ficar</strong> — o definitivo. É nele que tudo é reunido e são
              as chaves nacionais (CPF, CNS) dele que permanecem como principais.
            </Item>
            <Item>
              <strong>Cadastro que será absorvido</strong> — o duplicado. Ele não é apagado: fica
              desativado e apontando para o definitivo, para que qualquer link antigo continue achando
              a pessoa certa.
            </Item>
          </Lista>
          <P>
            Errou a ordem? O botão <BotaoRef>Trocar lados</BotaoRef> inverte os dois.
          </P>
        </>
      ),
    },
    {
      id: 'resolver-campos',
      titulo: 'Decidir dado a dado o que prevalece',
      busca: 'campo divergente resolução qual valor fica nome telefone endereço',
      conteudo: (
        <>
          <P>
            A tela mostra os dois cadastros e lista só os <strong>dados que estão diferentes</strong>.
            Para cada um, você clica no valor que deve prevalecer — o do cadastro que fica (já vem
            marcado) ou o do absorvido. O que for igual nos dois não aparece: não há o que decidir.
          </P>
          <Callout tipo="regra">
            CPF, CNS e data de nascimento <strong>não</strong> entram nessa escolha: são a identidade
            da pessoa. O cadastro que fica mantém os seus; os do absorvido são guardados como
            secundários (nada se perde), para que uma próxima importação ainda reconheça a pessoa.
          </Callout>
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
            Se os dois cadastros têm <strong>CPF (ou CNS) diferentes</strong>, a tela mostra um aviso
            vermelho. Documentos diferentes podem significar que são <strong>pessoas diferentes</strong>
            {' '}— e unir pessoas diferentes mistura dois prontuários, o que não se desfaz num clique.
          </P>
          <P>
            Nesse caso só dá para continuar depois de marcar <em>“Confirmo que é a mesma pessoa”</em>.
            Na dúvida, não una: confira antes com o documento em mãos.
          </P>
        </>
      ),
    },
    {
      id: 'o-que-e-movido',
      titulo: 'O que é movido e o registro',
      busca: 'laudos solicitações conversas histórico clínico auditoria histórico de alterações',
      conteudo: (
        <>
          <P>
            Antes de confirmar, a tela mostra <strong>o que será movido</strong> para o cadastro que
            fica: laudos, solicitações de exame, regulações, conversas de WhatsApp, acervo de exames e
            o restante. O histórico clínico (atendimentos, diagnósticos, medicações, documentos) é
            movido junto.
          </P>
          <P>
            Depois de <BotaoRef>Unificar cadastros</BotaoRef> e confirmar, a ação fica registrada no
            <strong> Histórico de alterações</strong> dos dois cadastros (quem fez, quando e o quanto
            foi movido) — a trilha que explica, semanas depois, o que aconteceu ali.
          </P>
          <Callout tipo="atencao">
            Só quem tem permissão de <strong>edição</strong> em Pacientes vê o botão e consegue
            unificar.
          </Callout>
        </>
      ),
    },
  ],
};
