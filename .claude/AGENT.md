# AGENT.md

## Project
Multi-alpha financial prediction platform combining statistical signals, fundamentals, institutional flow, sentiment, and internal human forecasts.

The system generates weekly predictions, aggregates signals into an alpha score, and produces portfolio weights.

## Core Alpha Signals
- Fundamental delta
- Market covariance delta
- Pairs delta
- Sector relative delta
- Institutional flow delta (based on public fund holdings and liquidity constraints)
- Public sentiment delta
- Internal member sentiment (user forecasts)

Signals are normalized and combined using covariance-aware weighting.

## Human Forecasting
Invited users submit weekly probabilistic forecasts (1–99%) for model-generated questions.

Questions are generated where the model has high uncertainty or signal disagreement.

User forecasts become another signal in the alpha model.

## Portfolio Construction
Combined alpha → portfolio weight using a smooth mapping (e.g. sigmoid/tanh).

Risk controls should include liquidity awareness and diversification.

## Diagnostics
System should visualize:
- signal correlations
- signal interaction heatmap
- signal contribution to final alpha

## Backend Rules (.NET)

Controllers must define request and response records **directly inside the controller**.

Do NOT create separate model scripts for request DTOs.

Example:

public record SubmitPredictionRequest(Guid QuestionId, double Probability);

This keeps request contracts local to their endpoints.

## Principle
Keep models simple, interpretable, and avoid unnecessary abstraction.