// Hl7.Fhir.Model define um recurso FHIR "Task" que colide com System.Threading.Tasks.Task.
// O alias global garante que "Task" sem qualificação seja sempre o de async.
global using Task = System.Threading.Tasks.Task;
