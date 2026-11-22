import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from '../../src/components/StudySessions';

// UX fix 5: after a failed submit, the cursor goes to the first field to fix.
describe('UX fix 5: focus moves to the first invalid field', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });

  beforeEach(() => {
    global.fetch = jest.fn(() => json(200, []));
  });

  afterEach(() => {
    delete global.fetch;
  });

  it('focuses the first empty field when the browser-side validation fails', async () => {
    render(<StudySessions />);
    await screen.findByRole('button', { name: 'Create session' });
    fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    // Course is filled in, so Topic is the first field to fix.
    expect(document.activeElement).toBe(screen.getByLabelText('Topic'));
  });

  it('focuses the field the API rejected', async () => {
    render(<StudySessions />);
    await screen.findByRole('button', { name: 'Create session' });
    fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });
    fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'Testing' } });
    fireEvent.change(screen.getByLabelText('Location'), { target: { value: 'MIF, room 401' } });
    fireEvent.change(screen.getByLabelText('Starts at'), { target: { value: '2030-01-01T18:00' } });
    fireEvent.change(screen.getByLabelText('Host name'), { target: { value: 'Ieva' } });
    global.fetch.mockImplementationOnce(() =>
      json(400, { errors: { Location: ['Location is already booked at that time.'] } }));

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    await screen.findByText('Location is already booked at that time.');
    expect(document.activeElement).toBe(screen.getByLabelText('Location'));
  });
});
