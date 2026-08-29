# ADR-0002: Monolito modular

- **Fecha:** 2026-08-22
- **Estado:** Aceptado

## Contexto

El sistema es un proyecto académico individual para una empresa pequeña, en red local, con módulos
relacionados que comparten datos y transacciones.

## Decisión

Se construye una sola aplicación desplegable, separada en Domain, Application, Infrastructure y Web.
Los módulos se organizan internamente por capacidad del negocio y respetan dependencias dirigidas.

## Consecuencias

- Despliegue, depuración y transacciones son sencillos.
- Las fronteras se verifican con pruebas de arquitectura.
- No se agregan microservicios, bus de eventos, CQRS, MediatR, AutoMapper o repositorio genérico.
- Un módulo futuro solo se separaría tras demostrar una necesidad operativa real.
